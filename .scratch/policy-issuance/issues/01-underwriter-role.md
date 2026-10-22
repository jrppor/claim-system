# 01: บทบาท Underwriter

**What to build:** เพิ่มบทบาทที่สี่ Underwriter: Admin สร้างผู้ใช้ Underwriter ได้ มีบัญชี demo ใน seed มี policy สิทธิ์ใหม่ เมนูและหน้าแสดงตามบทบาท (Garage Staff และ Insurance Adjuster เข้าหน้าของ Underwriter ไม่ได้) และ Admin อ่านอย่างเดียวในส่วนนี้

**Blocked by:** Controller refactor (ADR 0005) ต้องเสร็จก่อน (เงื่อนไขภายนอก ไม่ใช่ ticket ในชุดนี้)

**Status:** ready-for-agent

- [ ] สร้างผู้ใช้ Underwriter ผ่านหน้าจัดการผู้ใช้ได้ และ login ได้
- [ ] endpoint ที่ต้องใช้บทบาทนี้ได้ 403 สำหรับบทบาทอื่น และ 401 เมื่อไม่ login
- [ ] เมนูของ Underwriter แสดงเฉพาะหน้าที่เกี่ยวข้อง
- [ ] README ระบุบัญชี demo ใหม่
- [ ] test ผ่าน seam HTTP
