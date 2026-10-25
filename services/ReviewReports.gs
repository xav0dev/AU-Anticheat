// AU-Anticheat optional Google Apps Script review inbox. GPL-3.0.
// NOT deployed by the installer. See DOCUMENTATION.md before using.
// Use a SEPARATE PRIVATE spreadsheet, never the public ban-list spreadsheet.
// Reports are untrusted allegations. Only an owner's explicit Sheet approval
// can publish a community ban through the separately installed edit trigger.
// Run this manually as the owner once, after reviewing Google's permissions.
// A standalone new spreadsheet starts private. No sharing APIs are used.
function setupReviewInbox() {
  const properties = PropertiesService.getScriptProperties();
  const existing = properties.getProperty('REVIEW_SHEET_ID');
  if (existing) {
    console.log('Existing inbox: https://docs.google.com/spreadsheets/d/' + existing + '/edit');
    return; // Never replace, clear, or duplicate an existing inbox.
  }
  const book = SpreadsheetApp.create('AU-Anticheat Reports');
  const sheet = book.getSheets()[0];
  sheet.setName('Review');
  sheet.getRange(1,1,1,5).setValues([['received_utc','friend_code','claim','version','review_status']]).setFontWeight('bold');
  sheet.setFrozenRows(1);
  sheet.autoResizeColumns(1,5);
  properties.setProperty('REVIEW_SHEET_ID', book.getId());
  console.log('Private review inbox: ' + book.getUrl());
}

function doPost(event) {
  const reply = value => ContentService.createTextOutput(JSON.stringify(value))
    .setMimeType(ContentService.MimeType.JSON);
  if (!event || !event.postData || typeof event.postData.contents !== 'string' || event.postData.contents.length > 1024)
    return reply({accepted:false});
  let input;
  try { input = JSON.parse(event.postData.contents); } catch (_) { return reply({accepted:false}); }
  if (!input || typeof input.friend_code !== 'string' || typeof input.version !== 'string' ||
      !/^[a-z0-9]{1,24}#[0-9]{1,10}$/.test(input.friend_code) ||
      !/^[0-9A-Za-z.\-]{1,32}$/.test(input.version)) return reply({accepted:false});
  // Ignore submitted reasons, formulas and arbitrary fields. Nothing received
  // from a remote client can approve its own report or supply authenticated identity.
  const properties = PropertiesService.getScriptProperties();
  const id = properties.getProperty('REVIEW_SHEET_ID');
  if (!id) return reply({accepted:false});
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(2000)) return reply({accepted:false});
  try {
    const day = new Date().toISOString().substring(0,10);
    const previous = properties.getProperty('QUOTA_DAY');
    const count = previous === day ? Number(properties.getProperty('QUOTA_COUNT') || '0') : 0;
    if (!Number.isFinite(count) || count >= 100) return reply({accepted:false});
    const cache = CacheService.getScriptCache();
    if (cache.get('review:' + input.friend_code)) return reply({accepted:false});
    const sheet = SpreadsheetApp.openById(id).getSheetByName('Review');
    // Create the sheet and headers manually. This endpoint never edits sharing
    // settings, creates sheets, overwrites history or writes to the public list.
    if (!sheet || sheet.getLastRow() >= 5001) return reply({accepted:false});
    const expected = ['received_utc','friend_code','claim','version','review_status'];
    if (sheet.getRange(1,1,1,5).getDisplayValues()[0].join('|') !== expected.join('|'))
      return reply({accepted:false});
    sheet.appendRow([new Date().toISOString(), input.friend_code,
      'User-requested review; sender attribution unverified', input.version, 'pending']);
    properties.setProperties({QUOTA_DAY:day,QUOTA_COUNT:String(count+1)});
    cache.put('review:' + input.friend_code,'1',3600);
    return reply({accepted:true});
  } catch (_) { return reply({accepted:false}); }
  finally { lock.releaseLock(); }
}

function doGet() {
  // Never expose the private inbox over this web app.
  return ContentService.createTextOutput('AU-Anticheat review endpoint. POST only.');
}

// Run once as the owner. This installs an authorized Sheets edit trigger;
// receiving a report never fires it and the public POST cannot approve reports.
function installApprovalAutomation() {
  const p = PropertiesService.getScriptProperties();
  const reviewId = p.getProperty('REVIEW_SHEET_ID');
  const publicId = p.getProperty('PUBLIC_BAN_SHEET_ID') || '1X6mvEPqhUeBf-mEluQJ4S0BbBkwVGVMIeYUNCCTIny0';
  if (!reviewId || reviewId === publicId) throw new Error('Use separate private review and public ban workbooks.');
  const review = SpreadsheetApp.openById(reviewId).getSheetByName('Review');
  const publicSheet = approvalPublicSheet_(publicId);
  approvalHeaders_(review, ['received_utc','friend_code','claim','version','review_status']);
  approvalHeaders_(publicSheet, ['friend_code','status','reason','expires_utc']);
  const feedback = review.getRange(1,6,1,2).getDisplayValues()[0];
  if (feedback.some((v,i) => v && v !== ['sync_result','synced_utc'][i]))
    throw new Error('Columns F and G are occupied. No existing data was overwritten.');
  review.getRange(1,6,1,2).setValues([['sync_result','synced_utc']]).setFontWeight('bold');
  review.setColumnWidth(6,340); review.setColumnWidth(7,190);
  let trigger = ScriptApp.getProjectTriggers().find(t => t.getHandlerFunction() === 'onReviewDecision' &&
    t.getTriggerSourceId() === reviewId && t.getEventType() === ScriptApp.EventType.ON_EDIT);
  if (!trigger) trigger = ScriptApp.newTrigger('onReviewDecision').forSpreadsheet(reviewId).onEdit().create();
  p.setProperties({PUBLIC_BAN_SHEET_ID:publicId, APPROVAL_TRIGGER_UID:String(trigger.getUniqueId())});
  console.log('Approval automation installed. Edit review_status to approved to publish one ban; rejected/pending add nothing. Existing rows were not processed.');
}

function approvalHeaders_(sheet, expected) {
  if (!sheet || sheet.getRange(1,1,1,expected.length).getDisplayValues()[0].join('|') !== expected.join('|'))
    throw new Error('Unexpected Sheet headers. Nothing published.');
}
function approvalPublicSheet_(id) {
  const sheet = SpreadsheetApp.openById(id).getSheets().find(s => s.getSheetId() === 0);
  if (!sheet) throw new Error('Public ban-list tab gid=0 was not found.');
  return sheet;
}
function approvalCode_(value) {
  const code = String(value || '').trim().toLowerCase();
  return /^[a-z0-9]{1,24}#[0-9]{1,10}$/.test(code) ? code : null;
}

function onReviewDecision(event) {
  const p = PropertiesService.getScriptProperties();
  const reviewId = p.getProperty('REVIEW_SHEET_ID'), publicId = p.getProperty('PUBLIC_BAN_SHEET_ID');
  if (!event || !event.range || !event.source || !event.triggerUid ||
      String(event.triggerUid) !== p.getProperty('APPROVAL_TRIGGER_UID') ||
      event.source.getId() !== reviewId || !publicId || publicId === reviewId) return;
  const sheet = event.range.getSheet();
  if (sheet.getName() !== 'Review' || sheet.getParent().getId() !== reviewId ||
      event.range.getColumn() > 5 || event.range.getLastColumn() < 5) return;
  approvalHeaders_(sheet, ['received_utc','friend_code','claim','version','review_status']);
  if (sheet.getRange(1,6,1,2).getDisplayValues()[0].join('|') !== 'sync_result|synced_utc')
    throw new Error('Run installApprovalAutomation to prepare feedback columns.');
  const first = Math.max(2,event.range.getRow()), last = Math.min(sheet.getLastRow(),event.range.getLastRow());
  if (first > last) return;
  if (last-first+1 > 100) { sheet.getRange(first,6).setValue('Not synced: approve at most 100 rows per edit.'); return; }
  const lock = LockService.getScriptLock();
  if (!lock.tryLock(20000)) { sheet.getRange(first,6).setValue('Not synced: busy. Set pending, then approved to retry.'); return; }
  try {
    // Read decisions after obtaining the lock; stale event.value is not authority.
    let publicSheet;
    for (let row=first; row<=last; row++) {
      const cells = sheet.getRange(row,1,1,5), data = cells.getDisplayValues()[0];
      const decision = String(data[4]).trim().toLowerCase();
      if (decision !== 'approved') {
        sheet.getRange(row,6).setValue(decision === 'rejected' ? 'Rejected: no ban added. Existing bans are unchanged.' : 'Not approved: no ban added.');
        continue;
      }
      try {
        if (cells.getFormulas()[0][1] || cells.getFormulas()[0][4]) throw new Error('Use literal friend codes and decisions, not formulas.');
        const code = approvalCode_(data[1]);
        if (!code) throw new Error('Invalid friend code.');
        if (code === 'auqacheck#0000000000' || String(data[2]).startsWith('SELF-TEST'))
          throw new Error('Test records cannot create public bans.');
        if (!publicSheet) {
          const candidate = approvalPublicSheet_(publicId);
          approvalHeaders_(candidate, ['friend_code','status','reason','expires_utc']);
          if (candidate.getLastColumn() > 4 || candidate.getLastRow() > 2001)
            throw new Error('Public list exceeds the supported format or size.');
          publicSheet = candidate;
        }
        const outcome = publishApprovedCode_(publicSheet,code);
        sheet.getRange(row,6,1,2).setValues([[outcome,new Date().toISOString()]]);
      } catch (error) {
        sheet.getRange(row,6).setValue('Not synced: '+String(error.message || 'Check service execution log.').substring(0,180));
      }
    }
  } finally { lock.releaseLock(); }
}

function publishApprovedCode_(sheet, code) {
  const last = sheet.getLastRow();
  const rows = last > 1 ? sheet.getRange(2,1,last-1,4).getDisplayValues() : [];
  const matches=[];
  rows.forEach((r,i) => { if (approvalCode_(r[0]) === code) matches.push(i); });
  if (matches.length > 1) throw new Error('Duplicate friend codes exist in the public list. Resolve them first.');
  const values = [code,'ban','Approved by AU-Anticheat list maintainer',''];
  if (matches.length === 1) {
    const index=matches[0], current=rows[index];
    if (current[1] !== 'ban' && current[1] !== 'watch') throw new Error('Existing public entry has an invalid status.');
    const expiry = current[3] === '' ? Infinity : Date.parse(current[3]);
    if (current[3] && (!current[3].endsWith('Z') || !Number.isFinite(expiry))) throw new Error('Existing public entry has an invalid expiry.');
    if (current[1] === 'ban' && expiry > Date.now()) return 'Already banned: existing reason and expiry preserved.';
    sheet.getRange(index+2,1,1,4).setValues([values]);
    SpreadsheetApp.flush();
    return 'Published: existing watch/expired entry changed to ban (no expiry).';
  }
  if (last >= 2001) throw new Error('Public list is full (2,000 entries).');
  sheet.appendRow(values);
  SpreadsheetApp.flush();
  return 'Published: ban added (no expiry).';
}
