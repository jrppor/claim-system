Status: ready-for-agent

# รูปความเสียหายของ Claim (Claim Photo)

## Problem Statement

Garage Staff ตรวจสภาพรถแล้วต้องอธิบายความเสียหายเป็นข้อความอย่างเดียว Insurance Adjuster จึงไม่มีหลักฐานภาพให้ตรวจ ต้องขอรูปนอกระบบ ทำให้การพิจารณาช้าและไม่มีบันทึกว่าใครแนบหลักฐานอะไรเมื่อไร

## Solution

Garage Staff แนบรูปถ่ายความเสียหายกับ Claim ได้ในช่วงที่ยังแก้ข้อมูลได้ Insurance Adjuster และ Admin ดูรูปในแกลเลอรีของหน้ารายละเอียด Claim ได้ รูปถูกล็อกเมื่อ Claim ถูก Approved ทุกการอัปโหลดและลบบันทึกลง Claim History

## User Stories

1. As a Garage Staff, I want to upload a Claim Photo to a Claim, so that the Insurance Adjuster can see the damage
2. As a Garage Staff, I want to upload several photos in one go, so that I don't repeat the action for each file
3. As a Garage Staff, I want to upload photos while the Claim is Draft, so that I can collect evidence before submitting
4. As a Garage Staff, I want to upload photos while the Claim is Submitted, UnderReview or ChangesRequested, so that I can answer the Adjuster's questions
5. As a Garage Staff, I want to delete a photo I uploaded by mistake, so that wrong evidence is not left on the Claim
6. As a Garage Staff, I want to be told clearly when a file is rejected and why, so that I can fix it
7. As a Garage Staff, I want a message when the Claim already has 20 photos, so that I know the limit
8. As a Garage Staff, I want a message when a file is larger than 10 MB, so that I can resize it
9. As a Garage Staff, I want only JPEG, PNG and WebP accepted, so that unusable files are not attached
10. As a Garage Staff, I want a file renamed to .jpg but actually a document to be rejected, so that disguised files cannot be attached
11. As a Garage Staff, I want a short caption on each photo (optional), so that I can say which part of the car it shows
12. As a Garage Staff, I want thumbnails in the gallery, so that the page loads quickly
13. As a Garage Staff, I want to click a thumbnail to see the full photo, so that I can inspect detail
14. As an Insurance Adjuster, I want to see the photos of a submitted Claim, so that I can judge the Repair Estimate
15. As an Insurance Adjuster, I want to view photos at full size, so that I can check detail
16. As an Insurance Adjuster, I want to be unable to upload or delete photos, so that evidence is not altered by the reviewer
17. As an Insurance Adjuster, I want to be unable to see the photos of a Draft Claim, so that unfinished work stays private
18. As an Admin, I want to view photos read-only, so that I can support users
19. As an Admin, I want to be unable to upload or delete photos, so that I cannot alter cases
20. As a Garage Staff, I want photos locked after the Claim is Approved, so that evidence cannot change after the decision
21. As a Garage Staff, I want photos on a Rejected, Cancelled or Paid Claim to stay visible but read-only, so that history is preserved
22. As an Insurance Adjuster, I want Claim History to show who uploaded or deleted which photo and when, so that I can trace changes
23. As an Insurance Adjuster, I want photos accessible only to signed-in users with permission, so that customer evidence is not public
24. As a security-minded owner, I want original filenames never used on disk, so that path tricks and collisions cannot happen
25. As a security-minded owner, I want uploaded images served with the correct content type and no script execution, so that a malicious file cannot attack viewers
26. As a developer, I want file access behind one storage interface, so that I can swap disk for cloud storage later
27. As a developer, I want the storage folder configurable and outside the web root, so that files are never served directly
28. As a user, I want a conflict message if the Claim changed while I was uploading, so that I don't act on stale state
29. As a Garage Staff, I want a deleted photo's file removed from storage, so that disk space is not wasted
30. As an owner, I want a failed upload to leave no half-saved photo record, so that the gallery never shows broken images

## Implementation Decisions

บริบทและกฎ (ดู ADR 0006, 0005)
- Claim Photo เป็นข้อมูลของ Claim หนึ่งเรื่อง เก็บเมทาดาท: ชื่อไฟล์เดิม (เพื่อแสดงเท่านั้น), ชื่อที่เก็บจริง (GUID), ชนิดเนื้อหา, ขนาด, คำอธิบายสั้น (ไม่บังคับ), ผู้อัปโหลด, เวลา ตารางใหม่ `ClaimPhotos` สร้างด้วยสคริปต์ SQL ลำดับเลขต่อจากที่มี
- กฎอยู่ใน Domain: เพิ่มหรือลบรูปได้เฉพาะ Claim สถานะ Draft, Submitted, UnderReview, ChangesRequested และเฉพาะ Garage Staff ผ่านกฎสถานะเดียวกับที่ Claim ใช้อยู่ ไม่เกิน 20 รูปต่อ Claim
- การมองเห็น: ใช้กฎเดิมที่ Insurance Adjuster ไม่เห็น Draft ทั้ง Claim รวมถึงรูป ส่วน Admin อ่านอย่างเดียว
- การอัปโหลดและลบรูปเขียน Claim History (ใครทำ ทำอะไร เมื่อไร) ผ่านกลไกบันทึกประวัติเดิม แต่ **ไม่เปลี่ยนสถานะ Claim และไม่ส่งอีเมล** ถึง Customer
- ต้องส่ง `If-Match` ตามกฎเดิมของ Claim การเพิ่ม/ลบรูปไม่ทำให้สถานะ Claim เปลี่ยน แต่ตรวจ version ของ Claim ว่ายังตรง

ไฟล์
- ยอมรับ JPEG, PNG, WebP เท่านั้น ตรวจชนิดจากเนื้อไฟล์ (magic bytes) ไม่เชื่อนามสกุลหรือ Content-Type ที่ client ส่ง ไฟล์ละไม่เกิน 10 MB
- เก็บไฟล์บนดิสก์ในโฟลเดอร์นอก wwwroot ที่ตั้งค่าได้ ผ่าน interface `IFileStorage` ตัวเดียว ชื่อไฟล์ที่เก็บเป็น GUID ไม่ใช้ชื่อเดิมใน path
- เสิร์ฟรูปผ่าน endpoint ที่ตรวจสิทธิ์และการมองเห็น ไม่มี URL สาธารณะ ตอบด้วย Content-Type ที่ตรวจแล้วและ `X-Content-Type-Options: nosniff`
- ตอนอัปโหลดสร้าง thumbnail แบบย่อขนาดเดียว (ด้านยาวสุด 320 px) เก็บคู่กับต้นฉบับผ่าน `IFileStorage`
- ถ้าบันทึกเมทาดาทาหรือเขียนไฟล์ไม่สำเร็จ ต้องไม่ทิ้งทั้งแถวในตารางและไฟล์กำพร้า (เก็บไฟล์ก่อน แล้วบันทึกแถวใน transaction ถ้าล้ม ให้ลบไฟล์ที่เพิ่งเก็บ) ตอนลบ ลบแถวแล้วลบไฟล์ทั้งต้นฉบับและ thumbnail

API (เป็น Controller ตาม ADR 0005)
- อัปโหลดแบบ multipart รับได้หลายไฟล์ต่อคำขอ แต่ละไฟล์ผ่านการตรวจเอง ผลรวมถ้าเกิน 20 รูปปฏิเสธทั้งคำขอ (ไม่รับบางส่วน)
- รายการรูปของ Claim (เมทาดาท), ดึงต้นฉบับ, ดึง thumbnail, ลบรูป
- Error ทุกชนิดเป็น ProblemDetails ตามรูปแบบเดิม: ไฟล์ผิดชนิดหรือใหญ่เกิน 400 พร้อมระบุไฟล์และเหตุผล, ครบ 20 รูปหรือสถานะไม่อนุญาต 409, สิทธิ์ผิด 403, ไม่มี If-Match 428, version ชน 409 แบบ `urn:claim:version-conflict`

UI (ภาษาไทย)
- ส่วนแกลเลอรีในหน้ารายละเอียด Claim: thumbnail, คลิกดูรูปเต็ม, ปุ่มอัปโหลด (เลือกหลายไฟล์) และปุ่มลบ แสดงเฉพาะเมื่อบทบาทและสถานะอนุญาต แสดงข้อความภาษาไทยเมื่อไฟล์ถูกปฏิเสธและเมื่อ version ชน

## Testing Decisions

- seam เดียว: HTTP API ผ่าน `WebApplicationFactory` กับ fake ใน memory เหมือนเฟสแรก เพิ่ม `FakeFileStorage` และ fake repository ของรูป
- test ตรวจเฉพาะพฤติกรรมภายนอก: ตอบอะไร รูปปรากฏในรายการหรือไม่ ไฟล์ถูกเก็บหรือถูกลบใน fake หรือไม่ แถว Claim History และไม่มีอีเมลใน Outbox ไม่ตรวจ class ภายใน
- ครอบคลุม: อัปโหลดสำเร็จทั้งไฟล์เดียวและหลายไฟล์, ชนิดไฟล์จากเนื้อไฟล์ (ไฟล์ปลอมนามสกุล), ขนาดเกิน, ครบ 20 รูป (รวมกรณีหลายไฟล์ในคำขอเดียวที่ทำให้เกิน), ชื่อไฟล์แปลก/ซ้ำไม่กระทบ path, สถานะที่อนุญาตและไม่อนุญาตทุกสถานะ, สิทธิ์ทุกบทบาท (รวม 401/403), Adjuster เห็น 404 บน Draft, ลบแล้วไฟล์ถูกลบ, ความล้มเหลวกลางทางไม่ทิ้งข้อมูลกำพร้า, ไม่มี If-Match ได้ 428, version ชนได้ 409
- prior art: ชุด test ของ Claim Draft, Cancel และ Concurrency ใน `api/tests/Claim.Api.Tests`
- ไม่ครอบคลุม: การเขียนดิสก์จริง, SQL ของ Dapper และ UI ให้ตรวจมือกับ LocalDB และดิสก์จริงหนึ่งรอบ (อัปโหลด ดู ลบ) หลังทำเสร็จ

## Out of Scope

- วิดีโอ, PDF หรือเอกสารแนบอื่น
- หลายขนาดของ thumbnail และการ optimize/บีบอัดภาพ
- การแก้ไขรูป (หมุน ตัด) และการเก็บ EXIF/ตำแหน่ง
- สแกนไวรัส
- การย้ายไปเก็บบน Azure Blob หรือ S3 (แค่ต้องสลับได้ผ่าน `IFileStorage`)
- รูปประกอบ Invoice หรือ Repair Estimate
- Customer ดูรูป (Customer ไม่มี login)

## Further Notes

- ใช้คำศัพท์ตาม `GLOSSARY.md` (Claim Photo, Claim History, Garage Staff, Insurance Adjuster, Admin)
- ต้องทำหลังจาก Controller refactor (ADR 0005) เสร็จ เพื่อสร้าง endpoint ใหม่เป็น Controller เลย
- การอัปโหลดแบบ multipart ผ่าน Next.js proxy ต้องไม่จำกัดขนาดเกินที่กำหนด และต้องส่ง `If-Match` ต่อ
