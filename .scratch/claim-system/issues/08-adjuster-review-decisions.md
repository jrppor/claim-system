# 08: Adjuster พิจารณา Claim

**What to build:** Insurance Adjuster รับเรื่อง (UnderReview) แล้วอนุมัติด้วย Approved Amount พร้อมเหตุผล ปฏิเสธ (สถานะจบ) หรือขอให้แก้ไข (ChangesRequested) ซึ่งอู่ส่ง Repair Estimate revision ใหม่กลับมาให้พิจารณาซ้ำ ทุกขั้นบันทึก Claim History และส่งอีเมล

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] Approved Amount เกิน Coverage Limit ถูกปฏิเสธ และบันทึกแยกจาก Estimated Amount
- [ ] Claim ที่ Rejected เปิดซ้ำไม่ได้
- [ ] ChangesRequested ↔ UnderReview วนได้หลายรอบ และ Adjuster เห็นทุก revision
- [ ] ปุ่มใน UI แสดงเฉพาะที่บทบาทและสถานะนั้นทำได้
- [ ] test ผ่าน seam HTTP
