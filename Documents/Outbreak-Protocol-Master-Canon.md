# Outbreak Protocol — Master Canon

This Markdown is the single English runtime-content source for the three-chapter campaign. The three Word documents are reference archives only. Unity stores the same stable IDs in `CampaignContentCatalog`; the server replicates IDs and state, never document text.

Authoring: edit the `FILE_XX` paragraphs, numbered objective bullets or `DXX` dialogue bullets, then run **Tools → FPS → Campaign → Import English Canon**. The importer validates the complete ID set and file lengths before editing the catalog. It preserves icons, audio clips, Vietnamese translations and gameplay links, and never saves scenes. Review the VI translation when changing EN; VI is not the runtime language. Do not renumber IDs or change this small heading format.

This document specifies the target behavior, not a claim that every runtime/visual/multiplayer acceptance test has passed. Tank staging, UI prefab polish and multi-peer verification remain separate implementation work.

## Canon

T-9 is a fictional pathogen. The official Biohazard Response Team (BRT) deploys the four-person Vanguard squad to recover evidence from a quarantined factory. Shipment T9-17 leads to An Lac Care Institute, where altered records expose a hidden B2 level. Laboratory B2 stores the original E-02 archive. An anonymous client orders a sample-only export and removal of patient fields; Mira Shaw rejects the concealment order. Vanguard transmits a complete copy, secures both evidence cases and leaves. The quarantine remains. There is no cure, purge, NPC escort or “save every patient” ending.

| Player character | Runtime codename |
|---|---|
| Brimstone | Mason |
| Sage | Doc |
| Gekko | Vega |
| Clove | Raven |

Mira Shaw is the living BRT coordinator. Dr. Adrian Cole, Marcus Hale and Maya Reed are confirmed dead in the incident ledger and related records. They are not spawned as NPCs.

## Technology contract

Netcode for GameObjects 2.13.1 is server authority; Input System 1.20.0 supplies F/I/Esc; UGUI 2.5 + TextMeshPro present HUD, reader and debrief; AI Navigation 2.0.14 validates Tank arenas; Unity Test Framework 1.7.0 and Multiplayer Playmode 2.0.2 cover verification. ScriptableObjects hold this catalog and dialogue. Checkpoints continue the existing atomic JSON format. No Localization package or Timeline package is introduced.

## Chapter beats

1. **Cold Ledger / Factory:** isolate the production branch, engage the cold-store backup, start the generator, reconcile AL-04/K6, survive a mandatory Tank, secure the T9-17 case and open the service route.
2. **Those Who Never Left / An Lac Care Institute:** read the desk log, recover and install the service fuse, restore power, identify P046, prove the mortuary contradiction and call B2. The Tank can be evaded.
3. **The Unmapped Floor / Laboratory B2:** trace the transfer, read the index, allocate safe power, recover the original E-02 archive and case, then transmit while a Tank creates pressure. Killing that Tank is not required.

## Incident timeline

| Relative event | Record / consequence |
|---|---|
| Before the breach | Cole files a stop-work request; Hale retains records; Reed documents electrical and lift faults. |
| 02:40, incident night | AL-04/K6 is received at An Lac against C12/P046. |
| 03:05 | Public mortuary entry marks P046 deceased. This is an altered record, not confirmation of death. |
| 03:50 | The same P046 wristband is recorded in a living transfer to B2. |
| Containment failure | Quarantine shutters close; the unopened T9-17 case returns to factory storage. |
| After the breach | The E-02 incident ledger confirms Cole, Hale and Reed dead; patient outcomes beyond documented transfers remain unresolved. |
| Vanguard insertion | BRT recovers the chain of evidence across Factory, An Lac and B2. |
| Lab transmission | The anonymous client demands suppression; Mira rejects it; the full copy reaches outside custody. |

No calendar date, cure mechanism or unsupported patient outcome is implied by these records.

## Files (stable IDs 1–21)

A required file may be discovered from its physical source or its registered objective console. An optional file has only a physical source. Each body below is the full English reader text; the catalog also contains an EN/VI field pair.

### FILE_01 · Factory Utilities Procedure · FACTORY · REQUIRED

Night operator — Maya Reed. Isolate the PRODUCTION branch at the west breaker before touching the backup feed. Then engage BACKUP COLD STORE at the east breaker and start the generator. Do not isolate the cold store itself: its monitor and sealed cargo must remain refrigerated. The green generator lamp shares an alarm return and can light before the contactor closes; check the physical lever. Logistics must reconcile the shipment and seal independently. Keep the T9-17 case sealed until both checks are complete. Report any instruction to bypass this sequence in the shift ledger.

### FILE_02 · AL-04 Delivery Manifest · FACTORY · REQUIRED

Dispatch copy. Shipment AL-04; security seal K6; lot T9-17. Destination: An Lac Care Institute, service receiving entrance. Scheduled intake: 02:40, group C12. Security release: Marcus Hale. The public cargo description is temperature-controlled medical stock. Do not replace this page with the contractor's abbreviated receipt: that copy omits the receiving group and service entrance. A return instruction has been added in blue ink: if receiving refuses the sealed case, retain it in factory cold storage and leave the delivery record attached. Neither the seal number nor the destination may be changed by telephone.

### FILE_03 · B2 Transfer Addendum · FACTORY · REQUIRED

Route amendment to AL-04, seal K6. Receiving authority: Dr. Adrian Cole, technical level B2 beneath An Lac. The sample case was returned unopened to factory cold storage after the internal quarantine shutters closed. Its movement record remains valid and must travel with it. Patient-linked records stayed at the institute under group C12; they are not included in this case. B2 is absent from the public visitor directory. Any recovery team must retain this addendum with the original seal receipt. The contractor requested a sample-only return. Security has not authorized removal of the supporting records.

### FILE_04 · An Lac Security Desk Log · ASYLUM · REQUIRED

02:40 — Received AL-04 under seal K6 for group C12. Public admissions are suspended; technical intake continues through the service entrance. Record P046 against the receiving time, not the earlier appointment time. The upper records room holds the intake sheet. Access requires lower service power; the replacement fuse is in ground-floor maintenance storage. No patient has been discharged through this desk tonight. Staff asking for the public lift must be redirected to security. Marcus Hale instructed us to keep both the public log and technical supplement. The contractor asked for only the public log.

### FILE_05 · P046 Intake Record · ASYLUM · REQUIRED

Patient reference P046. Intake group C12. Receiving time 02:40. Linked consignment AL-04, lot T9-17. Observation status: fever and disorientation following the T-9 exposure protocol. Dr. Adrian Cole has requested immediate suspension of further exposure; approval remains unsigned. The public status reads RECOVERY TRANSFER. The internal destination reads B2 TECHNICAL OBSERVATION. Do not substitute P041, whose C08 intake was logged at 01:20. Family contact requests are held by administration. This sheet must accompany any movement order so that the receiving team can reconcile the patient reference with the original identity register.

### FILE_06 · Mortuary Transfer Discrepancy · ASYLUM · REQUIRED

P046 / C12. Mortuary entry: deceased, 03:05. Freight movement entry: living subject for B2 observation, 03:50. Both entries use the same patient reference and receiving wristband; this is not a second patient. The assigned mortuary drawer is empty. Maya Reed's lift note confirms the later trip carried an occupied observation trolley. The B2 access card is retained with this discrepancy report for an independent check. Do not resolve the conflict by deleting either entry. Whoever examines the records must see that a live transfer was logged after the reported death. — Security review copy.

### FILE_07 · B2 Archive Index · LABORATORY · REQUIRED

Restricted index, revision before contractor export. Trace lot T9-17 to intake group C12 and patient P046. The corresponding original archive is E-02. E-01 belongs to an earlier shipment and cannot establish this chain. E-03 is an equipment record without patient identities. The contractor export of E-02 excludes the identity table and Adrian Cole's stop-work request. Preserve the original directory, not just its export. Power the data path before opening the archive terminal. Security has placed the original under hold so that an outside investigator can compare what was recorded with what the client intended to receive.

### FILE_08 · Emergency Power Allocation · LABORATORY · REQUIRED

Maya Reed — B2 maintenance. Available emergency capacity: six units. SAFETY uses two, TEST RIG uses four, DATA uses two, and FREIGHT LIFT uses two. SAFETY must remain online throughout any switch. For records recovery, turn TEST RIG off, then enable DATA and FREIGHT LIFT. The resulting load is six; no reserve remains for experiments. The test indicator is not a safety interlock. An overload refuses the selection without damaging the archive. If interrupted, return to the panel and check its actual switches. Keep the escape lift powered until the last occupied trip is clear.

### FILE_09 · E-02 Original Incident Archive · LABORATORY · REQUIRED

Original E-02: lot T9-17, group C12, patient P046. The identity table, movement history and stop-work request are retained without redaction. Adrian Cole suspended the trial before containment failed. The later sample-only export was prepared under the client's instruction, not medical authority. Final incident ledger confirms the deaths of Dr. Adrian Cole, security chief Marcus Hale and technician Maya Reed during the breach; their signed records remain attached. Patient outcomes beyond the documented transfers are unresolved. Copy this complete archive to independent custody. Removing the identity fields would erase the link between the material and the people exposed.

### FILE_10 · Unsent Factory Letter · FACTORY · OPTIONAL

Ellie, I traded shifts again. Please do not wait at the bus stop tonight. They keep adding a sealed delivery after the ordinary trucks leave, and somebody has to stay with the temperature recorder until security signs. I used to know every driver by name. Now they speak to a caller before they speak to us. There is soup in the blue container, not the one with the cracked lid. Tell Ben I have not forgotten his Saturday match. I will come home when they let the last truck through.

### FILE_11 · Night Shift Handover · FACTORY · OPTIONAL

To the morning crew: the kettle trips the socket behind the time clock. Use the canteen socket until maintenance replaces it. Do not stack empty pallets against the service door, even if dispatch says the corridor is unused. We had to move all of them during yesterday's alarm. Maya left a proper electrical procedure beside the generator; read it instead of copying the last operator's switch positions. The spare locker keys are with security. If nobody relieves you, write your departure time honestly. We are not signing another complete shift that nobody actually worked.

### FILE_12 · Cold Store Return Memo · FACTORY · OPTIONAL

Security desk — Marcus Hale. The returned T9-17 case remains sealed. Photograph the seal before storage and attach the failed delivery receipt; do not classify it as unused stock. A caller asked whether destroying the temperature strip would simplify the return. I declined and recorded the call reference. Refrigeration is a custody requirement, not proof that the contents are safe. No factory employee is authorized to inspect the sample. If the client sends a replacement manifest, keep both versions. A cleaner document is not necessarily a more truthful one.

### FILE_13 · Ward Notice: Visiting Hours · ASYLUM · OPTIONAL

Visiting hours are suspended until further notice. Please leave letters with reception; staff will record the recipient and delivery time. Do not tell families that a patient has been discharged unless a named clinician has signed the discharge sheet. Administrative transfer is not discharge. The new technical observation stamps have caused repeated mistakes at this desk. If an outside caller asks for a room that no longer appears on the ward list, retain the request for review. Families should not have to provide the same name every time someone moves a folder.

### FILE_14 · A Letter Never Delivered · ASYLUM · OPTIONAL

Dear Daniel, the receptionist said your room changed and that visiting would resume next week. I left the photograph anyway. Your brother insists the dog recognizes you in it, which sounds impossible, but I am choosing to believe him. They would not let me leave your own blanket because everything has to be issued by the ward. I have written our number again on the back. You do not need to explain why you missed the call. Just ask someone to tell us which room you are in now. Love, Anna.

### FILE_15 · B2 Lift Maintenance Card · ASYLUM · OPTIONAL

Service entry — Maya Reed. The freight lift answered a lower-floor call after the public closure notice went up. That is not a fault: B2 remains connected to the emergency circuit. I replaced the landing lamp, not the card reader. Stop requesting a master bypass; the card is held by security for a reason. On descent, wait for the receiving door to finish opening before unloading. The cabin cannot safely carry a trolley across a half-open threshold. Leave this card at the panel so the next shift knows what was actually repaired.

### FILE_16 · Night Nurse Audio Transcript · ASYLUM · OPTIONAL

Recorded ward handover, transcript complete. I checked the trolley wristband twice. P046 was breathing when the doors closed. The screen upstairs had already changed to deceased. I asked whether we were taking the patient to emergency care, and security told me the receiving team would explain. Nobody did. I am leaving this recording with the paper handover because the electronic entry no longer accepts corrections from our ward. If you hear this on the next shift, preserve both times. Please do not sign my name under a report I did not write.

### FILE_17 · Operator Shutdown Checklist · LABORATORY · OPTIONAL

Before leaving the operations desk, record the active run, preserve its index and place any unsigned stop-work request with the original archive. Do not mark the trial complete merely because the sample has moved. Completion requires an authorized review of the incident record. Dr. Cole has returned this checklist twice with the same annotation: unresolved patient outcomes are not empty fields. The client export tool offers a clean summary automatically. That summary is a delivery convenience only. It must never replace the underlying record or become the only copy available to the next operator.

### FILE_18 · Contractor Export Email · LABORATORY · OPTIONAL

From: Contract Liaison. Subject: delivery scope. The receiving client expects material identifiers, storage condition and a confirmation of sample custody. Patient names, ward correspondence and internal objections fall outside the requested export. Please use the abbreviated profile already installed on the archive terminal. Reply from Adrian Cole: those fields establish what happened here. Omitting them changes the meaning of the result. I will not certify a sample-only record as the original. Forwarded response: certification can be resolved after collection. Keep this discussion off the public incident channel until our client has reviewed the package.

### FILE_19 · Calibration Bench Note · LABORATORY · OPTIONAL

Bench note — do not discard with consumables. The replacement display arrives with its brightness set to maximum, which makes an unlit warning look active through the scratched cover. Check the diagnostic page before logging a fault. This is a display problem, not evidence that containment is operating. I have tagged the affected unit and attached yesterday's reading so nobody has to reconstruct the comparison from memory. If the archive later shows a perfect sequence with no interruptions, compare it with the paper sheet. The instruments did not become more reliable when the reporting template changed.

### FILE_20 · Emergency Drill Review · LABORATORY · OPTIONAL

Drill review, security copy. Staff reached the freight landing, but two teams waited for contradictory clearance messages while the lift stood open. A local alarm did not reach the outside coordinator. Corrective action: confirm receipt beyond the facility before describing a transmission as delivered. A progress bar is not an acknowledgement. Marcus Hale requested an independent receiving contact and a printed boarding roster. The client declined the additional reporting step as unnecessary delay. This review remains open. Do not close it simply because the next drill has been removed from the calendar.

### FILE_21 · Maya's Final Shift Recording · LABORATORY · OPTIONAL

Personal recorder, final intact segment. This is Maya Reed. I have left the power note at the panel and the original maintenance sheets with the archive. Cole is still trying to stop the trial. Hale has ordered security to preserve the records, even if the client calls again. I can hear the shutters closing behind operations. If somebody reaches this desk later, do not trust the small export marked complete. The full directory is still here. Take the names with the numbers. We worked beside these people; they were never just a shipment. End of recovered segment.

## Key items

| ID | Item | Acquired | Consumed | Persistence |
|---|---|---|---|---|
| 1 | T9-17 Factory Evidence Case | FactoryCase | — | secured through ending |
| 2 | Service Fuse | AsylumFuse | AsylumInstall | removed after installation |
| 3 | B2 Access Card | AsylumTransfer | AsylumLift | removed after lift authorization |
| 4 | E-02 Laboratory Evidence Case | LabCase | — | secured through ending |

Items are team state derived from objective flags. They cannot be used remotely from Inventory, dropped, or lost when their holder dies.

## Objective truth table

- **1 ISOLATE PRODUCTION** — Open the production breaker. Keep the cold store connected to its backup circuit. Hint: Start with the west breaker marked PRODUCTION.
- **2 BACKUP COLD STORE** — Engage the cold-store backup feed after isolating production. Hint: The east breaker feeds the cold store; do not restart production.
- **3 START GENERATOR** — Start the generator only after the backup cold-store feed is engaged. Hint: Check the physical switch, not just the green lamp.
- **4 READ DELIVERY MANIFEST** — Record the AL-04 manifest before reconciling the shipping release. Hint: Compare shipment, seal and receiving time.
- **5 VERIFY SHIPPING RELEASE** — Select the shipment and seal, then its destination and receiving time. Hint: Match AL-04 / K6 with AN LAC / 02:40. Choices: AL-03 / K9 | AL-04 / K6 | AL-07 / K2. Secondary: NORTH DEPOT / 06:10 | AN LAC / 02:40 | AN LAC / 04:20.
- **6 SECURE T9-17 CASE** — Secure the returned case and its B2 transfer addendum. Preserve the seal. Hint: Complete Utilities and Logistics, then clear the Tank threat before collection.
- **7 OPEN SERVICE ROUTE** — Activate the service interlock. Prepare with the squad before beginning the hold. Hint: Gather living operators at the panel and confirm readiness.
- **8 READ SECURITY DESK LOG** — Record the technical intake hidden behind the public admissions log. Hint: Group C12 arrived at 02:40; the upper room holds the patient sheet.
- **9 RECOVER SERVICE FUSE** — Recover the spare fuse from ground-floor maintenance storage. Hint: The fuse is shared by the squad and can be installed by any living operator.
- **10 INSTALL SERVICE FUSE** — Install the recovered fuse at the lower service panel. Hint: Read the desk log and recover the fuse; neither is consumed by a wrong interaction.
- **11 RESTORE SERVICE POWER** — Bring the lower service circuit online after fitting the replacement fuse. Hint: The upper records room needs this circuit.
- **12 IDENTIFY PATIENT** — Select the record matching the desk log's group and receiving time. Hint: Match all three fields: P046 / C12 / 02:40. Choices: P041 / C08 / 01:20 | P046 / C12 / 02:40 | P064 / C12 / 04:20.
- **13 VERIFY MORTUARY TRANSFER** — Select the discrepancy that requires independent review. Retain the B2 card with the report. Hint: The same reference is listed dead before its live transfer. Choices: CONTRADICTION: DEATH 03:05 / LIVE TRANSFER 03:50 | CONSISTENT: DEATH FOLLOWED BY BODY COLLECTION | UNRELATED PATIENTS WITH SIMILAR REFERENCES.
- **14 CALL B2 FREIGHT LIFT** — Scan the B2 card. Prepare for the lift cycle and board with the squad. Hint: The card is spent at this panel; it cannot be used remotely from Inventory.
- **15 TRACE B2 TRANSFER** — Trace the institute transfer into the laboratory index. Hint: Follow T9-17, C12 and P046 together, not a sample identifier alone.
- **16 READ ARCHIVE INDEX** — Record the original-archive directory before searching the terminal. Hint: E-02 is the unredacted original, not the contractor export.
- **17 ALLOCATE EMERGENCY POWER** — Keep SAFETY online. Disable TEST RIG before enabling DATA and FREIGHT LIFT. Capacity: six units. Hint: SAFETY + DATA + FREIGHT LIFT = 2 + 2 + 2. TEST RIG stays off.
- **18 VERIFY ORIGINAL ARCHIVE** — Select the archive matching all three identifiers and preserve its identity table. Hint: Choose E-02 / T9-17 / C12 / P046. Choices: E-01 / T9-03 / C08 | E-02 / T9-17 / C12 / P046 | E-03 / EQUIPMENT ONLY.
- **19 SECURE E-02 CASE** — Secure the original archive in the laboratory evidence case. Retain the factory case as well. Hint: The original must be verified before the sealed case is released.
- **20 TRANSMIT COMPLETE EVIDENCE** — Prepare the squad, transmit a complete copy to BRT custody and call the freight lift. Do not delete patient fields. Hint: Keep both original cases. Transmission does not require eliminating every infected.

Utilities answer: production branch off → backup cold store on → generator. Shipping answer: AL-04/K6 and An Lac/02:40. Patient answer: P046/C12/02:40. Mortuary answer: deceased 03:05 and live transfer 03:50 is a contradiction. Laboratory power answer: mask 13 (SAFETY + DATA + FREIGHT LIFT, load 6; TEST RIG off). Archive answer: E-02/T9-17/C12/P046. Wrong choices only return feedback; they never consume a key item.

## Dialogue IDs

The server selects a dialogue ID; clients resolve the local catalog entry. Priority 2 is objective-critical/radio, 1 is life-state/special warning, 0 is ordinary bark. Equal-priority lines keep trigger order (client order before Mira's response). Enemy warning audio is never ducked by campaign radio. Late joiners do not replay old radio; current objective, Files and state remain authoritative.

- **D01 · Mira Shaw · Insertion · P2** — Vanguard, this is Mira Shaw, BRT coordination. The helicopter leaves after insertion. Recover the T9-17 case and its records. We need evidence, not a sample-only receipt.
- **D02 · Mira Shaw · Factory/Exploring · P2** — Cold Ledger. Restore cold-store power and reconcile the logistics release. Both branches are required; take them in either order.
- **D03 · Mira Shaw · Asylum/Exploring · P2** — An Lac Care Institute. Those Who Never Left. The factory case stays with Vanguard. Start with the security desk; find who received that shipment.
- **D04 · Mira Shaw · Laboratory/Exploring · P2** — The Unmapped Floor. B2 is below the institute, absent from its public plans. Trace P046 and find the unredacted E-02 archive.
- **D05 · Mira Shaw · FactoryGenerator · P2** — Backup power is stable. Preserve the seal and verify the logistics release before collecting T9-17.
- **D06 · Mira Shaw · FactoryShipping · P2** — AL-04, K6, An Lac at 02:40. That destination was missing from the client's recovery order. Keep the original manifest.
- **D07 · Mira Shaw · FactoryCase · P2** — T9-17 is secured. The addendum names B2 beneath An Lac. Use the service route and keep the case sealed.
- **D08 · Mira Shaw · AsylumAccess · P2** — C12 and 02:40 match the factory manifest. Restore service power and check the upper patient records.
- **D09 · Mira Shaw · AsylumPower · P2** — Service power is restored. The records room is accessible. The freight lift still needs security's B2 card.
- **D10 · Mira Shaw · AsylumPatient · P2** — P046 matches the intake. The public recovery entry points somewhere else internally. Cross-check the mortuary transfer.
- **D11 · Mira Shaw · AsylumTransfer · P2** — Death at 03:05, live transfer at 03:50. Preserve both entries. The B2 card gives you the route they concealed.
- **D12 · Mira Shaw · LabPower · P2** — Safety, data and freight lift are live. The test rig stays off. Recover the original record.
- **D13 · Mira Shaw · LabArchive · P2** — E-02 confirms T9-17, C12 and P046. Cole, Hale and Reed are confirmed dead in the incident ledger. Their records are what we can bring out.
- **D14 · Mira Shaw · LabCase · P2** — Both cases are secured. Copy the full archive to BRT custody before you leave.
- **D15 · Mira Shaw · Factory/Encounter · P2** — Service interlock is cycling. Hold the control room; the route will wait for the living squad.
- **D16 · Mira Shaw · Asylum/Encounter · P2** — The B2 lift is cycling. You can evade the Tank. Keep the route to the landing clear and recover anyone downed.
- **D17 · Mira Shaw · Laboratory/Encounter · P2** — Transmission is running. Keep moving and cover the data path. You do not need to kill the Tank to leave.
- **D18 · Mira Shaw · AwaitingParty · P2** — The route is ready. Bring every living operator into the boarding zone and hold together. Revive anyone downed first.
- **D19 · Mira Shaw · Asylum/Transitioning · P2** — Cabin secured. Descending to B2. Wait for the receiving door before moving out.
- **D20 · Mira Shaw · Completed · P2** — Vanguard is clear with both cases. The complete copy is in BRT custody. The facility remains quarantined. We have the evidence; the investigation starts now.
- **D21 · Mira Shaw · EvidenceTransmitted · P2** — Complete copy received outside the facility. Patient records included. Keep both cases and board the freight lift.
- **D22 · Anonymous Client · LabTransmit · P2** — Retain the sample. Remove patient identities and internal objections before transmission. Deliver the abbreviated package. That is the contract.
- **D23 · Mira Shaw · LabTransmit/AfterClientOrder · P2** — That is a concealment order, not a BRT directive. Preserve the original and transmit everything, including patient data. I will confirm receipt outside.
- **D24 · Mira Shaw · LifeState/Downed · P1** — Operator down. Cover the approach and get them back on their feet.
- **D25 · Mira Shaw · Special/Tank · P1** — Tank contact. Make room; do not let it pin the squad in a corridor.
- **D26 · Mira Shaw · Special/Screamer · P1** — Screamer nearby. Watch the flanks and listen for the call.
- **D27 · Mira Shaw · Special/Infector · P1** — Infector contact. Avoid exposure and keep a clear retreat.
- **D28 · Mira Shaw · LifeState/Revived · P1** — Operator recovered. Regroup before pushing forward.

## Event and encounter rules

The server publishes state transitions and dialogue IDs. Factory Tank is mandatory after Utilities and Logistics, in a validated arena, with no conflicting special encounter and at least one controllable living player. Asylum Tank is armed after the mortuary confirmation but may be evaded. Lab Tank starts when transmission begins and is pressure only. Unsafe spawn points defer the event rather than soft-locking the team.

At extraction, connected living players must be in the boarding zone. Downed players must be revived; dead or spectating players do not block boarding. A team wipe fails the campaign. Files and key items survive death. The retry restores the last valid checkpoint and starts a fresh attempt summary.

## Ending

Mira confirms that the complete copy reached BRT custody. Vanguard leaves with both cases. The facility remains quarantined and the investigation begins; the story never claims a cure, a total purge or a rescued patient population.
