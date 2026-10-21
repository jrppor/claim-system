# 12: Optimistic concurrency

**What to build:** Claims มี `rowversion` เมื่อสองคนแก้หรือเปลี่ยนสถานะพร้อมกัน คนหลังได้ HTTP 409 และ UI แจ้งให้โหลดใหม่ ใช้กับทุกการเปลี่ยนสถานะและการแก้ไข

**Blocked by:** 08

**Status:** ready-for-agent

- [ ] การเขียนด้วย version เก่าตอบ 409 ProblemDetails
- [ ] UI แสดงข้อความขัดแย้งภาษาไทยและโหลดข้อมูลใหม่ได้
- [ ] test ผ่าน seam HTTP โดย fake จำลองการชน (SQL จริงตรวจมือผ่าน Swagger)
