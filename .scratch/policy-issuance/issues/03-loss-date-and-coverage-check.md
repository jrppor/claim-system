# 03: Loss Date และตรวจช่วงคุ้มครองตอนส่ง Claim

**What to build:** Claim มี Loss Date (Draft บันทึกโดยไม่มีได้ Claim เดิมได้ค่าจากวันที่สร้าง) ตอนส่ง Claim ต้องมี Loss Date ไม่ใช่อนาคต อยู่ในช่วงคุ้มครองของ Policy และ Policy ไม่ถูกยกเลิกก่อนหรือตรงวันนั้น ข้อความปฏิเสธบอกเหตุผล (ไม่มี/นอกช่วง/ถูกยกเลิก) กฎอยู่ใน Domain ฟอร์มและหน้ารายละเอียด Claim แสดง Loss Date และช่วงคุ้มครอง การเลือก Policy ตอนเปิด Claim ใหม่แสดงเฉพาะ Active

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] ส่ง Claim ที่ไม่มี Loss Date, เป็นอนาคต, ก่อนวันเริ่ม, หลังวันสิ้นสุด หรือ Policy ถูกยกเลิกก่อน/ตรงวันนั้น ถูกปฏิเสธพร้อมเหตุผลที่ชัดเจน
- [ ] Policy ที่ถูกยกเลิกหลัง Loss Date ยังส่ง Claim ได้
- [ ] Draft บันทึกโดยไม่มี Loss Date ได้
- [ ] ค้นหา Policy ตอนเปิด Claim คืนเฉพาะ Active
- [ ] Claim เดิมที่ migrate แล้วยังส่งได้ (ตรวจมือกับ LocalDB)
- [ ] test ผ่าน seam HTTP ด้วยนาฬิกาจำลอง และ npm run build ผ่าน
