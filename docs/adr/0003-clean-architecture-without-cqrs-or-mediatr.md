# Clean Architecture 4 โปรเจค โดยไม่ใช้ CQRS และ MediatR

api/ แบ่งเป็น `Claim.Domain`, `Claim.Application`, `Claim.Infrastructure` และ `Claim.Api` ให้ Application เรียก Domain ผ่าน use case service ตรงๆ และ Infrastructure implement interface ของ repository ไม่ใช้ CQRS แยก command/query และไม่ใช้ MediatR (เวอร์ชันใหม่เป็นแบบมีลิขสิทธิ์) เพราะ prototype นี้ยังไม่ซับซ้อนพอจะคุ้มกับ ceremony ที่เพิ่ม

## Consequences

ถ้าภายหลังต้องแยก read model ให้ query หนักๆ สามารถนำ CQRS เข้ามาทีหลังได้ เพราะ use case แยกเป็น service อยู่แล้ว
