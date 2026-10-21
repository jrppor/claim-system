Status: ready-for-agent

# ระบบ Claim: เคลมประกันรถยนต์ระหว่างอู่กับบริษัทประกัน (เฟส 1)

## Problem Statement

เจ้าของระบบเป็นทั้งอู่ซ่อมรถและบริษัทประกันในรายเดียว ปัจจุบันไม่มีระบบกลางให้ Garage Staff กับ Insurance Adjuster ทำงานเคลมร่วมกัน การเปิดเรื่อง ประเมินราคา อนุมัติ ติดตามการซ่อม และปิดจ่าย กระจายอยู่หลายที่ ไม่มีประวัติว่าใครทำอะไรเมื่อไร และ Customer ไม่ได้รับแจ้งสถานะ นอกจากนี้ผู้พัฒนาต้องการผลงานต้นแบบที่ใช้ pattern แบบองค์กรใหญ่ เพื่อใช้แสดงเป็น portfolio

## Solution

เว็บแอปเดียวที่คนใช้ตามบทบาท Garage Staff เปิด Claim และจัดทำ Repair Estimate Insurance Adjuster ตรวจและอนุมัติ ปฏิเสธ หรือขอให้แก้ไข Admin จัดการผู้ใช้ ทุกการเปลี่ยนสถานะบันทึกใน Claim History และส่งอีเมลแจ้ง Customer ใช้ข้อมูลจำลองและไม่เชื่อมระบบประกันจริง UI เป็นภาษาไทย

## User Stories

ผู้ใช้และการเข้าสู่ระบบ
1. As an Admin, I want to create user accounts with a role, so that only people I authorise can use the system
2. As an Admin, I want to deactivate a user, so that former staff can no longer sign in
3. As an Admin, I want to reset a user's password, so that locked-out staff can get back in
4. As an Admin, I want to see the list of users with their roles and active state, so that I can audit access
5. As a user, I want to sign in with email and password, so that I can reach the pages for my role
6. As a user, I want my session to stay alive while I work, so that I'm not logged out every 15 minutes
7. As a user, I want to sign out, so that my session ends on a shared computer
8. As a user, I want to be stopped from pages my role does not allow, so that I can't act outside my duties
9. As a demo viewer, I want seeded accounts for each role, so that I can try every flow immediately

การเปิดและแก้ Claim (Garage Staff)
10. As a Garage Staff, I want to create a Claim as a Draft, so that I can save it before all details are known
11. As a Garage Staff, I want to look up a Policy by policy number or plate, so that I can attach the right coverage
12. As a Garage Staff, I want to record the vehicle, Customer contact email and damage description, so that the Claim is complete
13. As a Garage Staff, I want a readable Claim number such as CLM-2026-000001, so that I can quote it to the Customer
14. As a Garage Staff, I want to edit a Draft Claim, so that I can correct mistakes before submitting
15. As a Garage Staff, I want to submit a Draft Claim, so that the Insurance Adjuster can see it
16. As a Garage Staff, I want to be told what is missing when I submit an incomplete Claim, so that I can fix it
17. As a Garage Staff, I want to cancel a Claim before it is Approved, so that mistaken or withdrawn Claims are closed out
18. As a Garage Staff, I want cancelled Claims to stay visible but read-only, so that history is preserved

Repair Estimate
19. As a Garage Staff, I want to create a Repair Estimate with parts and labour lines, so that the Adjuster can review costs
20. As a Garage Staff, I want the Estimated Amount calculated from the lines, so that totals are never wrong by hand
21. As a Garage Staff, I want to submit a revised Repair Estimate after changes are requested, so that I can address the Adjuster's comments
22. As an Insurance Adjuster, I want to see every Repair Estimate revision, so that I can compare what changed
23. As an Insurance Adjuster, I want the latest revision clearly marked as the one under review, so that I don't approve a stale one
24. As a Garage Staff, I want a warning when the Estimated Amount exceeds the Policy's Coverage Limit, so that I can warn the Customer
25. As a Garage Staff, I want to still submit an estimate above the Coverage Limit, so that the Adjuster can decide the covered part

การพิจารณา (Insurance Adjuster)
26. As an Insurance Adjuster, I want to see only submitted Claims and never Drafts, so that I don't act on unfinished work
27. As an Insurance Adjuster, I want to pick up a Submitted Claim for review, so that colleagues know it is being handled
28. As an Insurance Adjuster, I want to approve a Claim with an Approved Amount and a reason, so that the garage can start repairs
29. As an Insurance Adjuster, I want the Approved Amount capped at the Coverage Limit, so that I can't approve beyond coverage
30. As an Insurance Adjuster, I want the Approved Amount recorded separately from the Estimated Amount, so that the Customer's share is clear
31. As an Insurance Adjuster, I want to reject a Claim with a reason, so that the garage and Customer know why
32. As an Insurance Adjuster, I want to request changes with comments, so that the garage can revise the estimate
33. As an Insurance Adjuster, I want rejected Claims to be final, so that a new Claim must be raised instead of reopening

การซ่อมและปิดงาน
34. As a Garage Staff, I want to mark an Approved Claim as In Repair, so that progress is visible
35. As a Garage Staff, I want to mark the Claim as Repaired when work is done, so that I can invoice
36. As a Garage Staff, I want to submit an Invoice for the actual repair cost, so that the insurer can pay
37. As an Insurance Adjuster, I want to confirm payment on a submitted Invoice, so that the Claim closes as Paid
38. As an Insurance Adjuster, I want to see the Invoice next to the Approved Amount, so that I can spot differences before paying
39. As any user, I want invalid transitions refused with a clear message, so that Claims can't skip steps

การติดตามและแจ้งเตือน
40. As a Customer, I want an email whenever my Claim changes status, so that I know where it stands
41. As a Customer, I want the email to state the Claim number, new status and any reason given, so that I understand what happened
42. As a system owner, I want status changes to succeed even when email sending is down, so that work isn't blocked
43. As a system owner, I want failed emails retried automatically, so that Customers are eventually notified
44. As a user, I want to see the full Claim History with who, when and why, so that I can trace every decision
45. As a developer, I want to inspect sent emails in Mailpit, so that I can test notifications without a real mail server

การค้นหาและภาพรวม
46. As a user, I want a dashboard with Claim counts by status for my role, so that I see where work is waiting
47. As a user, I want a Claim list with search by number, plate or Customer, so that I can find a Claim quickly
48. As a user, I want to filter the Claim list by status, so that I can focus on what needs action
49. As a user, I want paged lists, so that large lists stay fast
50. As a user, I want the Claim detail page to show only the actions my role and the Claim's status allow, so that I'm not offered impossible actions
51. As an Admin, I want read-only access to all Claims, so that I can support users without altering cases

ความถูกต้องและความปลอดภัย
52. As a user, I want a clear conflict message if someone else changed the Claim meanwhile, so that I don't overwrite their work
53. As a user, I want Thai labels and messages throughout the UI, so that it matches the working language
54. As a security-minded owner, I want passwords stored hashed and tokens kept out of browser scripts, so that credentials are protected
55. As a security-minded owner, I want refresh tokens rotated and revocable on sign-out, so that stolen tokens have limited use
56. As a developer, I want OpenAPI documentation, so that I can explore and manually verify endpoints
57. As a developer, I want structured logs and uniform error responses, so that failures are easy to diagnose

## Implementation Decisions

สถาปัตยกรรมและขอบเขต (ดู ADR 0001, 0003)
- ระบบเป็น single-tenant ไม่มี Garage หรือ Insurer เป็นข้อมูล ชื่ออู่และบริษัทประกันเป็น configuration
- Clean Architecture 4 โปรเจค: Domain, Application, Infrastructure, Api ใช้ use case service ตรงๆ ไม่ใช้ CQRS และไม่ใช้ MediatR
- Repository และ Unit of Work บน Dapper, FluentValidation, Serilog, ProblemDetails เป็นรูปแบบ error กลาง, Swagger/OpenAPI
- เจ้าของระบบเป็นรายเดียว UI ภาษาไทย ส่วนโค้ด ตาราง และ API เป็นภาษาอังกฤษ
- ใช้ .NET 10 สำหรับ API และ Next.js (App Router, TypeScript) กับ MUI สำหรับ UI

ฐานข้อมูล (ดู ADR 0004)
- DB ชื่อ `Claim` บน SQL Server LocalDB `(localdb)\MSSQLLocalDB` สร้างด้วยสคริปต์ SQL ลำดับเลข และสคริปต์ PowerShell ตัวเดียวที่สร้าง DB แล้วรันตามลำดับ ไม่ใช้ EF migrations
- ตารางหลัก: Users, Policies, Claims, ClaimHistory (เพิ่มต่อท้ายอย่างเดียว), RepairEstimates (เก็บทุก revision) พร้อมรายการอะไหล่และค่าแรง, Invoices, EmailOutbox
- ทุกตารางใช้ bigint identity เป็น key ภายใน Claim มีเลขที่อ่านง่ายรูปแบบ `CLM-yyyy-000001` จาก SQL Sequence
- Claims มีคอลัมน์ `rowversion` สำหรับ optimistic concurrency การชนกันต้องคืน HTTP 409
- seed ข้อมูล: ผู้ใช้เริ่มต้น 1 คนต่อบทบาท, Policy และ Customer จำลอง, ตัวอย่าง Claim ในหลายสถานะ

วงจรสถานะของ Claim (กฎอยู่ใน Domain)
- Draft → Submitted → UnderReview ↔ ChangesRequested → Approved | Rejected → InRepair → Repaired → InvoiceSubmitted → Paid (Closed)
- Cancelled ได้ก่อน Approved เท่านั้น Rejected เป็นสถานะจบและเปิดซ้ำไม่ได้
- ทุกการเปลี่ยนสถานะเขียน Claim History (ผู้ทำ เวลา เหตุผล) และแถว EmailOutbox ใน transaction เดียวกับการเปลี่ยนสถานะ
- Repair Estimate: ส่งใหม่หลัง ChangesRequested สร้าง revision ใหม่ ไม่เขียนทับ revision ล่าสุดคือฉบับที่พิจารณา
- Adjuster อนุมัติทั้งใบ ระบุ Approved Amount ก้อนเดียวพร้อมเหตุผล ไม่เกิน Coverage Limit ของ Policy และอาจน้อยกว่า Estimated Amount
- ถ้า Estimated Amount เกิน Coverage Limit ให้เตือนตอนส่งแต่ยังส่งได้

สิทธิ์ตามบทบาท
- Garage Staff: สร้างและแก้ Claim, Repair Estimate, อัปเดตการซ่อม, ส่ง Invoice, ยกเลิก Claim
- Insurance Adjuster: ดูเฉพาะ Claim ที่ส่งแล้ว (ไม่เห็น Draft), อนุมัติ ปฏิเสธ ขอแก้ไข ยืนยันจ่าย
- Admin: จัดการผู้ใช้ และดู Claim แบบอ่านอย่างเดียว

การยืนยันตัวตน
- JWT access token อายุ 15 นาที และ refresh token ที่ rotate ทุกครั้งที่ใช้ เก็บใน Redis (`localhost:6379`) ยกเลิกได้ตอน sign out
- รหัสผ่านเก็บแบบ hash ผู้ใช้สร้างโดย Admin เท่านั้น ไม่มีหน้าสมัครเอง
- UI เก็บ token ใน httpOnly cookie ผ่าน Next route handlers

อีเมล (ดู ADR 0002)
- Outbox Pattern: BackgroundService อ่าน EmailOutbox แล้วส่ง SMTP ไปที่ Mailpit `localhost:1025` (หน้าเว็บ 8025) ลองใหม่เมื่อพลาด
- Redis ใช้เฉพาะ refresh token ไม่ใช้เป็นคิวหรือ cache

API
- REST JSON ปิดด้วย ProblemDetails สำหรับ error ทุกชนิด (validation, 401, 403, 404, 409, ผิดกฎสถานะ)
- กลุ่ม endpoint: auth, users, claims (สร้าง แก้ ค้นหา แบ่งหน้า รายละเอียด), การเปลี่ยนสถานะแต่ละแบบ, repair estimates, invoices, dashboard summary

UI (6 หน้า)
- Login, Dashboard แยกตามบทบาท, รายการ Claim (ค้นหา กรอง แบ่งหน้า), รายละเอียด Claim (ข้อมูล Repair Estimate ทุก revision Claim History และปุ่มตามบทบาทและสถานะ), ฟอร์ม Claim/Estimate, จัดการผู้ใช้
- TanStack Query สำหรับดึงข้อมูล, React Hook Form + Zod สำหรับฟอร์ม

## Testing Decisions

- ทดสอบเฉพาะพฤติกรรมภายนอกผ่าน seam เดียว: HTTP API ของ Api project ด้วย `WebApplicationFactory` โดยสลับ repository, Unit of Work และตัวส่งอีเมลเป็น fake ใน memory ไม่แตะ DB จริงและไม่ใช้ Docker
- test ควรเรียก endpoint แล้วตรวจ response, สถานะ Claim, Claim History และแถว Outbox ที่ fake เก็บไว้ ไม่ตรวจ class ภายใน
- ครอบคลุม: วงจรสถานะและการปฏิเสธการข้ามขั้น, สิทธิ์ตามบทบาท (รวมกรณี 403), Repair Estimate revision, กฎ Approved Amount กับ Coverage Limit, ความขัดแย้งแบบ optimistic concurrency ผ่าน fake ที่จำลอง 409, validation และ ProblemDetails
- ไม่ครอบคลุม: SQL ของ Dapper (ตรวจมือผ่าน Swagger) และ UI
- ไม่มี prior art เพราะ repo ยังว่าง การตั้ง fake และ test host คือสิ่งที่ต้องสร้างเป็นอย่างแรก

## Out of Scope

เรื่องเหล่านี้ตัดไปเฟสถัดไป (บันทึกไว้ที่ `.scratch/phase-2-backlog.md` และต้องแจ้งผู้ใช้หลังจบเฟสนี้)
- อัปโหลดรูปความเสียหาย
- การคำนวณเบี้ยประกันและการออกกรมธรรม์ (เฟสนี้ seed Policy ไว้)
- การชำระเงินจริงและการเชื่อมระบบภายนอกหรือระบบประกันจริง
- Redis cache ข้อมูลอ้างอิง
- integration test ของ SQL กับ SQL Server จริง
- ระบบ multi-tenant (ดู ADR 0001) และ UI ฝั่ง Customer / login ของ Customer
- unit test ของ UI

## Further Notes

- ใช้คำศัพท์ตาม `GLOSSARY.md` (Claim, Policy, Customer, Repair Estimate, Invoice, Claim History, Estimated Amount, Approved Amount, Coverage Limit และบทบาททั้งสาม)
- Redis ที่รันอยู่ใช้ port 6379 ไม่ใช่ 6389 ตามที่เคยแจ้ง ให้ตั้งเป็น config ที่แก้ได้
- เครื่องพัฒนามี .NET 10 SDK, Node 24 และ LocalDB พร้อมแล้ว
- ไม่มีไฟล์ `triage-labels.md` ใน repo จึงใช้สตริง `ready-for-agent` ตามที่ skill ระบุ
