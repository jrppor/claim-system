Status: ready-for-agent

# เบี้ยประกันและการออกกรมธรรม์ (Quotation → Policy)

## Problem Statement

เฟสแรกใช้ Policy ที่ seed ไว้ จึงไม่มีทางสร้างลูกค้าใหม่หรือรถคันใหม่ในระบบ เจ้าของระบบไม่มีเครื่องมือคำนวณเบี้ย เสนอเบี้ย และออกกรมธรรม์ และ Claim ก็ไม่ตรวจว่ารถนั้นอยู่ในช่วงคุ้มครองตอนเกิดเหตุหรือไม่ ทำให้ส่ง Claim ภายใต้ Policy ที่หมดอายุหรือถูกยกเลิกได้

## Solution

เพิ่มบทบาท **Underwriter** ที่คำนวณ Premium จัดทำ Quotation ส่งให้ Customer ทางอีเมล และออก Policy (ช่วงคุ้มครอง 1 ปี) เมื่อยืนยันรับเบี้ย Claim ต้องระบุ Loss Date และส่งได้เฉพาะเมื่อ Loss Date อยู่ในช่วงคุ้มครองของ Policy ที่ Active ณ วันนั้น

## User Stories

1. As an Admin, I want to create a user with the Underwriter role, so that policy work can be assigned to the right people
2. As an Underwriter, I want to sign in and see only the pages for my role, so that I don't touch Claim decisions
3. As an Underwriter, I want to start a Quotation by entering vehicle, coverage class, Sum Insured and the driver's date of birth, so that I can price a policy
4. As an Underwriter, I want to select an existing Customer by email or enter a new one, so that I don't create duplicates
5. As an Underwriter, I want the Premium calculated from the rate table, so that prices are consistent
6. As an Underwriter, I want to see the calculation step by step (base rate, vehicle-age factor, driver-age factor, No-Claim Bonus, total), so that I can explain the price
7. As an Underwriter, I want the No-Claim Bonus worked out from the Customer's Paid Claims in the last 3 years, so that I don't type it by hand
8. As an Underwriter, I want a new Customer with no history to get no discount and no penalty, so that first-time customers are priced fairly
9. As an Underwriter, I want to choose among coverage classes 1, 2+, 3+ and 3, so that I can offer different products
10. As an Underwriter, I want a vehicle-age field validated against sensible bounds, so that nonsense data is not priced
11. As an Underwriter, I want a Sum Insured within a minimum and maximum for the class, so that unrealistic values are refused
12. As an Underwriter, I want the driver age limits enforced (for example 18 to 80), so that ineligible drivers are refused with a reason
13. As an Underwriter, I want to save a Quotation, so that I can send it later
14. As an Underwriter, I want a Quotation to carry a number, a 30-day validity date and a snapshot of the rates used, so that the price can be audited even if rates change
15. As an Underwriter, I want to send a Quotation to the Customer by email, so that the Customer sees the Premium and coverage
16. As a Customer, I want an email with the Premium, coverage class, Sum Insured and validity, so that I can decide
17. As an Underwriter, I want an expired Quotation to be refused for issuing, so that stale prices are not used
18. As an Underwriter, I want to recalculate an expired Quotation as a new one, so that I can re-offer after rates change
19. As an Underwriter, I want to issue a Policy from a valid Quotation after confirming "premium received", so that coverage starts
20. As an Underwriter, I want to choose the coverage start date (today or later, within a limit), so that coverage begins when agreed
21. As an Underwriter, I want the coverage period to be exactly one year from the start date, so that terms are uniform
22. As an Underwriter, I want a unique Policy number POL-yyyy-000001 on issue, so that I can reference it
23. As an Underwriter, I want the Coverage Limit of the Policy to equal the Sum Insured automatically, so that I don't enter it twice
24. As an Underwriter, I want a Quotation to be issuable only once, so that duplicate Policies are not created
25. As a Customer, I want an email when my Policy is issued with the number, dates and Premium, so that I have confirmation
26. As an Underwriter, I want to cancel a Policy with a reason, so that coverage can be ended
27. As an Underwriter, I want cancellation to take effect from a chosen date not before today, so that past coverage is not rewritten
28. As a Customer, I want an email when my Policy is cancelled, so that I know coverage has ended
29. As an Underwriter, I want a Policy to become Expired automatically after its end date, so that old Policies stop covering
30. As an Underwriter, I want the Policy and Quotation history (who did what, when), so that actions can be traced
31. As an Underwriter, I want to search Policies by number, plate or Customer, so that I can find them
32. As an Underwriter, I want to filter Policies by status Active, Expired or Cancelled, so that I can focus
33. As an Underwriter, I want to search Quotations by number, Customer or status, so that I can follow up
34. As an Underwriter, I want paged lists, so that large lists stay fast
35. As a Garage Staff, I want to find a Policy by number or plate when opening a Claim, so that I attach the right one
36. As a Garage Staff, I want only Active Policies offered for a new Claim, so that I don't pick an invalid one
37. As a Garage Staff, I want to enter the Loss Date when preparing a Claim, so that coverage can be checked
38. As a Garage Staff, I want a Draft Claim to be savable without a Loss Date, so that I can complete it later
39. As a Garage Staff, I want submission refused when the Loss Date is missing, so that I fix it
40. As a Garage Staff, I want submission refused when the Loss Date is before the coverage start or after the coverage end, so that out-of-period Claims are not sent
41. As a Garage Staff, I want submission refused when the Policy was cancelled on or before the Loss Date, so that cancelled coverage is respected
42. As a Garage Staff, I want a Loss Date in the future refused, so that impossible dates are not accepted
43. As a Garage Staff, I want the refusal message to name the reason (missing, outside period, cancelled), so that I know what to do
44. As an Insurance Adjuster, I want to see the Loss Date and the Policy period on the Claim, so that I can verify coverage
45. As an Insurance Adjuster, I want existing Claims to keep working after the upgrade, so that old data is not broken
46. As an Admin, I want existing seeded Policies to keep their Coverage Limit as Sum Insured, so that old Claims remain valid
47. As an Admin, I want existing Claims to receive a Loss Date from their creation date, so that they remain consistent
48. As an Admin, I want Underwriter pages hidden from Garage Staff and Insurance Adjuster, so that duties stay separate
49. As an Admin, I want read-only access to Quotations and Policies, so that I can support users
50. As a security-minded owner, I want rate values and Premium computed only on the server, so that clients cannot set their own price
51. As a user, I want a conflict message if someone else changed the Policy or Quotation meanwhile, so that I don't overwrite their work
52. As a developer, I want rates stored in a table with default values seeded, so that pricing can be adjusted by SQL script without code changes

## Implementation Decisions

บริบทและโมเดล (ดู ADR 0001, 0003, 0005 และ GLOSSARY: Policy, Quotation, Premium, Sum Insured, Coverage Limit, Loss Date, No-Claim Bonus, Underwriter)
- บทบาทใหม่ **Underwriter** เพิ่มเป็นบทบาทที่สี่ มี policy สิทธิ์ของตัวเองและ seed บัญชี demo หนึ่งบัญชี Admin ต้องสร้างผู้ใช้ Underwriter ได้และดู Quotation/Policy แบบอ่านอย่างเดียว Garage Staff กับ Insurance Adjuster ใช้งานหน้าของ Underwriter ไม่ได้
- **Quotation**: เลขที่เฉพาะ, Customer (เลือกที่มีหรือสร้างใหม่), ข้อมูลรถ, ชั้นประกัน (1, 2+, 3+, 3), Sum Insured, วันเกิดผู้ขับขี่หลัก, Premium พร้อมรายการคำนวณทีละขั้น, สแนปช็อตของอัตราที่ใช้, วันหมดอายุ (30 วันนับจากสร้าง), สถานะ (Draft, Sent, Issued, Expired)
- **Policy** เปลี่ยนจากข้อมูลอ้างอิงที่ seed เป็นข้อมูลที่ระบบออก: เพิ่ม Sum Insured, ชั้นประกัน, Premium, วันเริ่มและวันสิ้นสุดคุ้มครอง (1 ปี), สถานะ Active/Expired/Cancelled, วันที่ยกเลิกและเหตุผล, ผู้ออก อ้างอิง Quotation ที่เป็นต้นทาง เลข `POL-yyyy-000001` จาก SQL Sequence
- Coverage Limit ของ Policy ที่ออกใหม่เท่ากับ Sum Insured เสมอ ไม่ให้กรอกแยก
- **Claim** เพิ่ม Loss Date กฎใน Domain ตอนส่ง Claim (Draft → Submitted): ต้องมี Loss Date, ไม่เป็นอนาคต, อยู่ในช่วงคุ้มครองของ Policy และ Policy ต้องไม่ถูก Cancelled ในวันที่ตรงหรือก่อน Loss Date ถ้าไม่ผ่านปฏิเสธด้วยข้อความที่บอกเหตุผล Draft บันทึกได้โดยไม่มี Loss Date
- การเลือก Policy ตอนเปิด Claim ใหม่แสดงเฉพาะ Policy ที่ Active

การคำนวณเบี้ย
- กำหนดแน่นอนจากตารางอัตราใน DB (ไม่ฮาร์ดโค้ด) ปัจจัย: ชั้นประกัน (อัตราฐานเป็นเปอร์เซ็นต์ของ Sum Insured), อายุรถ (ตัวคูณตามช่วงปี), อายุผู้ขับขี่หลัก (ตัวคูณตามช่วงอายุ) และ No-Claim Bonus เป็นสูตรอย่างง่ายสำหรับ prototype ไม่ใช่สูตรของบริษัทใด ปัดเศษเป็นสตางค์ด้วยกฎเดียวกันทุกที่
- No-Claim Bonus คำนวณจากจำนวน Claim สถานะ Paid ของ Customer คนเดียวกันในระบบ ย้อนหลัง 3 ปี (ไม่มีเลย = ส่วนลดสูงสุด และลดหลั่นตามจำนวน) Customer ใหม่ไม่ได้รับทั้งส่วนลดหรือเบี้ยเพิ่ม
- Quotation เก็บสแนปช็อตอัตราและรายการคำนวณ เพื่อให้ผลไม่เปลี่ยนแม้ตารางอัตราถูกแก้ภายหลัง ไม่มีหน้าจอแก้อัตราในเฟสนี้ (แก้ด้วยสคริปต์ SQL)
- ขอบเขตการยอมรับ (อายุผู้ขับขี่ อายุรถ ช่วง Sum Insured ต่อชั้น) อยู่ในตารางอัตราเดียวกัน

วงจรชีวิตและการออก Policy
- Quotation: Draft → Sent (ส่งอีเมล) → Issued (ออก Policy) หรือ Expired (เกินวันหมดอายุ) ออก Policy ได้ครั้งเดียวต่อ Quotation และเฉพาะที่ยังไม่หมดอายุ
- ตอนออก Policy ผู้ออกยืนยัน "รับเบี้ยแล้ว" (บันทึกประวัติ ไม่มีการชำระเงินจริงและไม่มีสถานะรอจ่าย) เลือกวันเริ่มคุ้มครอง (วันนี้หรือหลังจากนั้นภายในขีดจำกัดที่ตั้งค่าได้)
- Policy: Active → Expired อัตโนมัติเมื่อพ้นวันสิ้นสุด หรือ → Cancelled โดย Underwriter พร้อมเหตุผลและวันที่มีผล (ไม่ก่อนวันนี้) ไม่มีต่ออายุ endorsement หรือคืนเบี้ย
- การหมดอายุประเมินจากวันที่ ณ เวลาตรวจ (ไม่ต้องมีงานเบื้องหลังรันเปลี่ยนสถานะ ให้สถานะที่แสดงและกฎ Claim อ่านจากวันที่) เพื่อให้ผลถูกต้องแม้ระบบไม่ได้รัน
- ทุกการเปลี่ยนที่สำคัญของ Quotation และ Policy บันทึกประวัติแบบเพิ่มต่อท้ายอย่างเดียว (ใคร ทำอะไร เมื่อไร เหตุผล) ใช้แนวเดียวกับ Claim History
- อีเมลถึง Customer ผ่าน Outbox (ADR 0002) ตอนส่ง Quotation, ออก Policy และยกเลิก Policy ในธุรกรรมเดียวกับการเปลี่ยนสถานะ

การย้ายข้อมูลเดิม
- Policy ที่ seed ไว้เดิม: Sum Insured เท่ากับ Coverage Limit เดิม ตั้งช่วงคุ้มครองครอบคลุมวันที่ของ Claim เดิมทั้งหมด (เช่น เริ่มก่อนวันที่สร้าง Claim แรกสุด และสิ้นสุดไกลพอ) และสถานะ Active เพื่อไม่ให้ Claim เดิมพัง
- Claim เดิมทุกเรื่องได้ Loss Date เท่ากับวันที่สร้าง
- สคริปต์ย้ายข้อมูลต้องรันซ้ำได้โดยไม่เสียหาย

API (Controller ตาม ADR 0005, `If-Match` ตามกฎเดิมสำหรับสิ่งที่มี version)
- Quotation: ขอคำนวณ/สร้าง, แก้ไขตอน Draft, ส่ง, ออก Policy, รายการ (ค้นหา กรอง แบ่งหน้า), รายละเอียด
- Policy: รายการ (ค้นหา กรอง แบ่งหน้า), รายละเอียด, ยกเลิก
- ค้นหา Customer จากอีเมลสำหรับเลือกหรือสร้าง
- Claim: รับและแสดง Loss Date ในสร้าง/แก้/รายละเอียด
- สิทธิ์: Underwriter ทำงานกับ Quotation/Policy; Admin อ่านอย่างเดียว; Garage Staff ค้นหา Policy ที่ Active ได้เหมือนเดิม; Adjuster เห็น Loss Date และช่วงคุ้มครองบน Claim
- Error ทุกชนิดเป็น ProblemDetails รูปแบบเดิม

UI (ภาษาไทย)
- หน้าของ Underwriter: รายการ/สร้าง Quotation พร้อมแสดงรายละเอียดการคำนวณทีละขั้น, รายการ Policy, ยกเลิก Policy; ฟอร์ม Claim เพิ่มช่อง Loss Date; หน้ารายละเอียด Claim แสดง Loss Date และช่วงคุ้มครอง; เมนูแสดงตามบทบาท

## Testing Decisions

- seam เดียว: HTTP API ผ่าน `WebApplicationFactory` กับ fake ใน memory เหมือนเฟสแรก (เพิ่ม fake repository ของ Quotation, Policy และตารางอัตรา และนาฬิกาจำลองที่ควบคุมได้)
- ตรวจพฤติกรรมภายนอก: เบี้ยที่คืนจาก endpoint และรายการคำนวณทีละขั้นต้องตรงค่าที่คำนวณมือจากอัตราที่ seed ไม่เขียน unit test ของตัวคำนวณแยก
- ครอบคลุม: การคำนวณทุกชั้นและช่วงขอบ (อายุผู้ขับขี่/อายุรถ/Sum Insured ที่ขอบ), No-Claim Bonus (ไม่มี Claim, มี Claim Paid ใน 3 ปี, นอก 3 ปี, Claim ที่ไม่ใช่ Paid ไม่นับ, Customer ใหม่), สแนปช็อตอัตราไม่เปลี่ยนเมื่ออัตราถูกแก้, Quotation หมดอายุออก Policy ไม่ได้, ออกซ้ำไม่ได้, Coverage Limit เท่ากับ Sum Insured, ช่วงคุ้มครอง 1 ปี, ยกเลิก Policy, Policy หมดอายุตามนาฬิกาจำลอง, กฎ Loss Date ทุกกรณี (ไม่มี, อนาคต, ก่อนเริ่ม, หลังสิ้นสุด, Policy ถูกยกเลิกก่อนหรือตรงวันนั้น, ถูกยกเลิกหลังวันเกิดเหตุยังส่งได้), Draft ไม่มี Loss Date บันทึกได้, อีเมลใน Outbox ทั้งสามชนิด, สิทธิ์ทุกบทบาท (401/403 รวม Admin อ่านอย่างเดียว), การเลือก Policy ตอนเปิด Claim เห็นเฉพาะที่ Active, ความขัดแย้ง version
- prior art: ชุด test ของ Repair Estimate, Adjuster review, Cancel และ Concurrency ใน `api/tests/Claim.Api.Tests`
- ไม่ครอบคลุม: SQL ของ Dapper และ UI ต้องตรวจมือ โดยเฉพาะสคริปต์ย้ายข้อมูล (Sum Insured, ช่วงคุ้มครอง, Loss Date) ให้รันกับ LocalDB ที่มีข้อมูลเฟสแรกจริง ตรวจจำนวนแถวก่อนหลัง และรันซ้ำสองรอบ และตรวจว่า Claim เดิมยังส่งได้

## Out of Scope

- การชำระเบี้ยจริงและสถานะรอชำระ
- การต่ออายุกรมธรรม์, endorsement และการคืนเบี้ย
- หน้าจอจัดการอัตราเบี้ย (แก้ด้วยสคริปต์ SQL ไปก่อน)
- Customer ล็อกอินเพื่อดู Quotation หรือ Policy
- การพิมพ์หรือสร้างไฟล์ PDF ของ Quotation และกรมธรรม์
- สูตรเบี้ยจริงของบริษัทประกันใดๆ และการเชื่อมระบบภายนอก
- ผู้ขับขี่หลายคน, อุปกรณ์เสริม, ผู้รับผลประโยชน์
- ยกเลิกย้อนหลังและการคืนเบี้ยจากการยกเลิก

## Further Notes

- ใช้คำศัพท์ตาม `GLOSSARY.md` เวอร์ชันล่าสุด (นิยาม Policy และ Coverage Limit เปลี่ยนไปแล้ว)
- ต้องทำหลัง Controller refactor (ADR 0005) และควรทำหลัง spec รูปความเสียหาย
- ข้อควรระวัง: นี่เป็นครั้งแรกที่การออก Policy ต้องอ่านข้อมูล Claim (ผ่าน No-Claim Bonus) ให้อ่านผ่านจุดเดียวที่ชัดเจน ไม่เจาะตารางข้ามไปมา
- สูตรอัตราและค่าเริ่มต้นที่ seed เป็นข้อมูลสาธิต ไม่ควรถูกเข้าใจว่าเป็นการคิดเบี้ยจริง
