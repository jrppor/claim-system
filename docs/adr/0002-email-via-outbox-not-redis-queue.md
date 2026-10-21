# ส่งอีเมลด้วย Outbox Pattern ไม่ใช้ Redis เป็นคิว

อีเมลแจ้งสถานะถึง Customer ถูกเขียนลงตาราง `EmailOutbox` ใน transaction เดียวกับการเปลี่ยนสถานะ Claim แล้ว BackgroundService อ่านไปส่งผ่าน SMTP และลองใหม่เมื่อพลาด ทำให้การเปลี่ยนสถานะไม่ล้มเมื่อ SMTP ล่ม และไม่มีอีเมลหายหรือส่งโดยที่สถานะไม่เปลี่ยน

Redis ใช้เฉพาะเก็บ refresh token ไม่ใช้เป็นคิว เพราะคิวใน Redis ไม่อยู่ใน transaction เดียวกับ DB จึงเสี่ยงสถานะกับอีเมลไม่ตรงกัน

## Considered Options

- ส่งอีเมลแบบ synchronous ใน request: ง่ายสุด แต่ SMTP ล่มแล้วทำให้การเปลี่ยนสถานะล้มตาม
- คิวบน Redis: ไม่ atomic กับ DB
