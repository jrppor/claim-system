# 06: ส่ง Quotation ถึง Customer

**What to build:** Underwriter ส่ง Quotation เป็นอีเมลถึง Customer ผ่าน Outbox (เบี้ย ชั้นประกัน Sum Insured วันหมดอายุ) สถานะเป็น Sent พร้อมบันทึกประวัติ Quotation ที่เกิน 30 วันถือเป็น Expired จากวันที่ ณ เวลาตรวจ

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] ส่งแล้วสถานะเป็น Sent มีแถว Outbox ในธุรกรรมเดียวกับการเปลี่ยนสถานะ และอีเมลเป็นภาษาไทยมีข้อมูลครบ
- [ ] SMTP ล่มแล้วการส่งยังสำเร็จและอีเมลถูกส่งทีหลัง
- [ ] Quotation เกิน 30 วันแสดงเป็น Expired และส่ง/ออก Policy ไม่ได้
- [ ] ส่งซ้ำหรือส่ง Quotation ที่ออก Policy แล้วได้ 409
- [ ] test ผ่าน seam HTTP
