# 07: Repair Estimate

**What to build:** Garage Staff สร้างและส่ง Repair Estimate ด้วยรายการอะไหล่และค่าแรง ระบบคำนวณ Estimated Amount เก็บทุก revision เตือนเมื่อเกิน Coverage Limit ของ Policy แต่ยังส่งได้ และ Insurance Adjuster เห็นทุก revision โดยมีฉบับล่าสุดเป็นฉบับที่พิจารณา

**Blocked by:** 06

**Status:** ready-for-agent

- [ ] Estimated Amount คำนวณจากรายการเสมอ ไม่รับค่าจาก client
- [ ] ส่งใบใหม่สร้าง revision ใหม่และไม่เขียนทับของเดิม
- [ ] ยอดเกิน Coverage Limit มีคำเตือนใน response และ UI แต่ส่งได้
- [ ] test ผ่าน seam HTTP
