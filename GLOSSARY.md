# Claim

ระบบเคลมประกันรถยนต์ที่เชื่อมงานซ่อมของอู่กับการอนุมัติของบริษัทประกัน ดำเนินการโดยเจ้าของระบบเพียงรายเดียว

## Language

**Claim**:
คำขอเรียกร้องค่าซ่อมรถหนึ่งคันจากความเสียหายหนึ่งเหตุการณ์ ภายใต้ Policy หนึ่งฉบับ ตั้งแต่เปิดเรื่องจนปิดจ่าย
_Avoid_: Case, ticket, เรื่อง

**Policy**:
กรมธรรม์ประกันของ Customer ที่ระบุรถที่คุ้มครองและวงเงินคุ้มครอง เป็นข้อมูลอ้างอิงที่ Claim ชี้ถึง
_Avoid_: Insurance, contract

**Customer**:
เจ้าของรถและผู้ถือ Policy ไม่มีบัญชีเข้าสู่ระบบ รับแจ้งสถานะ Claim ทางอีเมลเท่านั้น
_Avoid_: Client, owner, user

**Repair Estimate**:
ใบประเมินราคาซ่อมที่อู่จัดทำสำหรับ Claim ประกอบด้วยรายการอะไหล่และค่าแรง เพื่อให้ Insurance Adjuster พิจารณา
_Avoid_: Quote, quotation, ใบเสนอราคา

**Invoice**:
เอกสารเรียกเก็บค่าซ่อมจริงที่อู่ส่งเมื่อซ่อมเสร็จ เพื่อให้ประกันยืนยันการจ่ายและปิด Claim
_Avoid_: Bill, receipt

**Claim History**:
บันทึกที่เพิ่มต่อท้ายอย่างเดียวของทุกการเปลี่ยนสถานะของ Claim ระบุผู้ทำ เวลา และเหตุผล
_Avoid_: Audit log, timeline

**Estimated Amount**:
ยอดรวมที่อู่ประเมินใน Repair Estimate
_Avoid_: Quoted price

**Approved Amount**:
ยอดที่ Insurance Adjuster อนุมัติจ่าย ไม่เกินวงเงินคุ้มครองของ Policy และอาจน้อยกว่า Estimated Amount ส่วนต่างเป็นความรับผิดชอบของ Customer
_Avoid_: Payout, settled amount

**Coverage Limit**:
วงเงินคุ้มครองสูงสุดของ Policy ต่อ Claim หนึ่งเรื่อง
_Avoid_: Cap, max coverage

## Roles

**Garage Staff**:
ผู้ใช้ฝั่งอู่ ทำหน้าที่เปิด Claim จัดทำ Repair Estimate อัปเดตความคืบหน้าการซ่อม และส่ง Invoice
_Avoid_: Mechanic, shop user

**Insurance Adjuster**:
ผู้ใช้ฝั่งประกัน ทำหน้าที่ตรวจ Repair Estimate แล้วอนุมัติ ปฏิเสธ หรือขอให้แก้ไข และยืนยันการจ่าย
_Avoid_: Approver, insurer user

**Admin**:
ผู้ใช้ที่จัดการบัญชีผู้ใช้ของระบบ
_Avoid_: Superuser, root
