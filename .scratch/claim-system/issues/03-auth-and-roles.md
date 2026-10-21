# 03: เข้าสู่ระบบและสิทธิ์ตามบทบาท

**What to build:** ผู้ใช้ login ด้วยอีเมลและรหัสผ่าน ได้ JWT 15 นาทีและ refresh token ที่ rotate ทุกครั้ง (เก็บใน Redis) sign-out แล้ว token ใช้ไม่ได้ ผู้ใช้ seed 1 คนต่อบทบาท หน้า Login และการกันหน้า/endpoint ตามบทบาท (Garage Staff, Insurance Adjuster, Admin) token เก็บใน httpOnly cookie ผ่าน Next route handlers

**Blocked by:** 01, 02

**Status:** ready-for-agent

- [ ] login สำเร็จ/ล้มเหลวตอบถูกต้อง รหัสผ่านเก็บแบบ hash
- [ ] refresh แล้ว token เก่าใช้ซ้ำไม่ได้ และ sign-out เพิกถอน refresh token
- [ ] endpoint ที่ไม่ใช่สิทธิ์ของบทบาทตอบ 403 และไม่ login ตอบ 401
- [ ] UI ไม่แสดงหน้าที่บทบาทนั้นเข้าไม่ได้
- [ ] test ผ่าน seam HTTP ครอบ login, refresh, sign-out และ 401/403
