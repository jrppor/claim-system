# 01: อัปโหลดและดูรูปความเสียหายหนึ่งรูป

**What to build:** Garage Staff แนบ Claim Photo หนึ่งรูปกับ Claim แล้วเห็นใน gallery ของหน้ารายละเอียด ครบเส้น: ตาราง `ClaimPhotos`, `IFileStorage` (ดิสก์ตามโฟลเดอร์ที่ตั้งค่าได้ และ fake ใน memory สำหรับ test) ตาม ADR 0006, ตรวจชนิด JPEG/PNG/WebP จากเนื้อไฟล์และขนาดไม่เกิน 10 MB, เก็บด้วยชื่อ GUID, เสิร์ฟผ่าน endpoint ที่ตรวจสิทธิ์ (nosniff, ไม่มี URL สาธารณะ), กฎสถานะที่อนุญาต (Draft, Submitted, UnderReview, ChangesRequested), Adjuster ไม่เห็นรูปของ Draft, Admin อ่านอย่างเดียว, ต้องส่ง `If-Match`, บันทึก Claim History โดยไม่เปลี่ยนสถานะและไม่ส่งอีเมล และ gallery ใน UI ภาษาไทย

**Blocked by:** Controller refactor (ADR 0005) ต้องเสร็จก่อน (เงื่อนไขภายนอก ไม่ใช่ ticket ในชุดนี้)

**Status:** ready-for-agent

- [ ] อัปโหลดรูปที่ถูกต้องสำเร็จ เห็นในรายการ และดึงรูปได้
- [ ] ไฟล์ที่นามสกุล .jpg แต่เนื้อไม่ใช่ภาพถูกปฏิเสธ 400 พร้อมเหตุผล และไฟล์เกิน 10 MB ถูกปฏิเสธ
- [ ] ชื่อไฟล์เดิมไม่ถูกใช้เป็น path และชื่อซ้ำหรือชื่อแปลกไม่กระทบ
- [ ] สถานะที่ไม่อนุญาตได้ 409, บทบาทอื่นนอกจาก Garage Staff ได้ 403 ตอนอัปโหลด, ไม่ login ได้ 401, ไม่มี If-Match ได้ 428, version ชนได้ 409
- [ ] Adjuster ได้ 404 บนรูปของ Claim ที่เป็น Draft และเห็นได้เมื่อส่งแล้ว, Admin เห็นอย่างเดียว
- [ ] Claim History มีแถวการอัปโหลด ไม่มีอีเมลใน Outbox และสถานะ Claim ไม่เปลี่ยน
- [ ] test ผ่าน seam HTTP กับ FakeFileStorage; ตรวจมือกับดิสก์จริงหนึ่งรอบ (อัปโหลด ดู); npm run build ผ่าน
