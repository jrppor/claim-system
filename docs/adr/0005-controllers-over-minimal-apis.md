---
status: accepted
---

# ใช้ Controller (`[ApiController]`) แทน Minimal API

เฟสแรกสร้าง endpoint ด้วย Minimal API โดยไม่ได้ตัดสินใจร่วมกัน ภายหลังเจ้าของโปรเจคเลือก Controller เพราะเป้าหมายคือ pattern แบบองค์กรใหญ่ที่คนทั่วไปคุ้นเคย endpoint จัดกลุ่มเป็นคลาสละหนึ่งทรัพยากร ใช้ `[Authorize(Policy=...)]` เป็น attribute และ Controller บางเฉียบ เรียก service ใน Application เท่านั้น ไม่มี logic ธุรกิจใน Controller

## Considered Options

- คง Minimal API: โค้ดสั้นกว่า แต่ไม่ใช่รูปแบบที่ทีมองค์กรส่วนใหญ่ใช้และอ่านยากกว่าเมื่อ route เยอะ
