# 06: ส่ง Claim พร้อม Claim History และอีเมล

**What to build:** Garage Staff ส่ง Draft เป็น Submitted โดยมีกฎวงจรสถานะอยู่ใน Domain ทุกการเปลี่ยนสถานะเขียน Claim History (ผู้ทำ เวลา เหตุผล) และแถว EmailOutbox ใน transaction เดียวกัน BackgroundService ส่งอีเมลหา Customer ผ่าน Mailpit พร้อม retry (ADR 0002) หน้ารายละเอียดแสดง History

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] ส่ง Claim ที่ข้อมูลไม่ครบถูกปฏิเสธพร้อมบอกว่าขาดอะไร
- [ ] การข้ามขั้นหรือเปลี่ยนสถานะที่ไม่อนุญาตถูกปฏิเสธด้วย ProblemDetails
- [ ] การเปลี่ยนสถานะสำเร็จแม้ SMTP ล่ม และอีเมลถูกส่งเมื่อกลับมา
- [ ] อีเมลระบุเลข Claim สถานะใหม่ และเหตุผล และเห็นใน Mailpit
- [ ] test ผ่าน seam HTTP โดย fake เก็บ Outbox ให้ตรวจ
