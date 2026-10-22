# 07: ออก Policy จาก Quotation

**What to build:** Underwriter ยืนยัน "รับเบี้ยแล้ว" (บันทึกประวัติ ไม่มีการชำระเงินจริง) เลือกวันเริ่มคุ้มครอง (วันนี้หรือหลังจากนั้นภายในขีดจำกัดที่ตั้งค่าได้) แล้วได้ Policy เลข `POL-yyyy-000001` จาก SQL Sequence ช่วงคุ้มครองหนึ่งปี Coverage Limit เท่ากับ Sum Insured ออกซ้ำจาก Quotation เดิมไม่ได้ Quotation ที่หมดอายุออกไม่ได้ ส่งอีเมลยืนยันถึง Customer และ Garage Staff เปิด Claim กับ Policy ใหม่ได้ครบเส้นทาง Quotation → Policy → Claim

**Blocked by:** 02, 06

**Status:** ready-for-agent

- [ ] ออก Policy สำเร็จ ได้เลขที่เรียงต่อ ไม่ซ้ำ ช่วงคุ้มครองและ Coverage Limit ถูกต้อง Quotation เป็น Issued
- [ ] ออกซ้ำและออกจาก Quotation ที่หมดอายุหรือยังเป็น Draft ได้ 409
- [ ] วันเริ่มคุ้มครองในอดีตหรือไกลเกินขีดจำกัดถูกปฏิเสธ
- [ ] อีเมลถูกเขียนใน Outbox ในธุรกรรมเดียวกัน; ประวัติบันทึกผู้ออกและการยืนยันรับเบี้ย
- [ ] ใช้ Policy ใหม่เปิด Claim และส่งได้ตามกฎ Loss Date; สิทธิ์เฉพาะ Underwriter
- [ ] ตรวจมือกับ LocalDB จริงหนึ่งรอบ (Sequence เลข Policy); test ผ่าน seam HTTP
