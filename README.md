# Angela-Translator
โปรแกรมแปลข้อความจากหน้าจอแบบ Selection Translate ที่ออกแบบมาสำหรับเกม Visual Novel, RPG, Emulator หรือสื่อบันเทิงต่าง ๆ โดยผู้ใช้สามารถเลือกเฉพาะพื้นที่บนหน้าจอที่ต้องการแปลได้โดยตรง

โปรแกรมจะจับข้อความจากพื้นที่ที่เลือกด้วย OCR แล้วนำข้อความทั้งหมดในพื้นที่นั้นมารวมและเรียงต่อกันเป็นประโยคก่อนทำการแปล เพื่อให้ได้ผลลัพธ์ที่เหมาะกับบทสนทนาในเกมหรือข้อความที่ถูกแบ่งหลายบรรทัดบนหน้าจอ

### คำศัพท์เฉพาะ
- OCR คือ โปรแกรมที่ใช้อ่านข้อความจากรูปภาพ
- Ollama คือ โปรแกรม Runtime ที่ใช้สำหรับ Run AI ทั้งในรูปแบบ Local หรือ Cloud ซึ่งในโปรเจคนี้จะนำมาใช้สำหรับการแปล

### รองรับการแปล Dialog เกมแบบต่อเนื่อง

- เลือกพื้นที่ Subtitle หรือ Dialog เพียงครั้งเดียว
- กดแปลต่อเนื่องได้ทันที
- ไม่ต้องลากเลือกพื้นที่ใหม่ทุกประโยค
- เหมาะสำหรับเกม Visual Novel และ RPG

## Quick Start

1. Download [AngelaTranslator-Setup.exe](https://github.com/PromnarongPomarsa/Angela-Translator/releases/download/v2.1.1/AngelaTranslator-Setup.exe) จาก Release
2. ติดตั้งโปรแกรม (Angela + Ollama)
3. Login Ollama
5. เปิด Angela Translator
6. กด Alt + S เพื่อเลือกพื้นที่
7. กด Alt + D เพื่อแปล

⚠️ **หมายเหตุ**
AngelaTranslator-Setup.exe จะติดตั้งทั้งโปรแกรม Angela และ Ollama ด้วย แต่หากเครื่องของผู้ใช้งานมี Ollama อยู่แล้วจะข้ามขั้นตอนการติดตั้งในส่วนนี้ออกไป
   
# Important Notes

โปรแกรมนี้ถูกออกแบบมาสำหรับการแปลข้อความเฉพาะส่วนของหน้าจอ เน้นใช้ในการแปลเป็นประโยค Subtitle หรือ Dialog Sentence จึงมีข้อแนะนำดังนี้

เพื่อประสิทธิภาพที่ดีที่สุด:

- ควรเลือกเฉพาะพื้นที่ข้อความที่ต้องการแปล
- หากใช้อ่านเอกสาร (Document / PDF / Website) ควรเลือกแปลทีละ Paragraph
<img width="1032" height="637" alt="image" src="https://github.com/user-attachments/assets/e9c52ef5-d64e-40fc-a48e-409d149ecea5" />


- การเลือกพื้นที่ขนาดใหญ่เกินไปอาจทำให้ OCR และการแปลมีความแม่นยำน้อยลง
- จำเป็นต้องทำการ Login Ollama เสมอในการใช้งาน

# Hotkeys
- **Select Screen Area:**        `Alt + S` 
- **Translate Selected Area:**   `Alt + D`
- **Quick Language Change**      `Alt + C`
- **Exit or Cencel translation:** `ESC`

# Features
- Selection Translate แบบเลือกพื้นที่บนหน้าจอ
- OCR จับข้อความจากหน้าจอด้วย PaddleOCR
- แปลข้อความด้วย AI Models ผ่าน Ollama
- รองรับการใช้งานแบบ Hotkey
- หากใช้กับเกม Dialog subtitle ให้เลือกพื้นที่ Selection เพียงแค่ครั้งเดียวและกดแปลได้เลยตลอด ไม่ต้องเลือกใหม่
- Run แบบ System Tray เพื่อทำงานบนพื้นหลัง
- การตั้งค่าสำหรับการ Run Program ทันทีเมื่อเปิดเครื่องคอมพิวเตอร์

# How It Works
- ผู้ใช้เลือกพื้นที่บนหน้าจอ
- โปรแกรมจับภาพเฉพาะบริเวณที่เลือก
- ตรวจจับข้อความ
- รวมข้อความทั้งหมดในพื้นที่ที่เลือกเข้าด้วยกัน
- ส่งข้อความไปยัง AI Model ผ่าน Ollama
- แสดงผลลัพธ์การแปลในกล่องข้อความ

# Technologies
- OCR Engine: PaddleOCR
- AI Runtime: Ollama
- UI Framework: WPF (.NET)

# Requirements
- Windows
- Ollama installed and running
- AI Translation Model installed in Ollama
- PaddleOCR dependencies

# Example Use Cases
- Translate Visual Novel dialogue
- Translate RPG in-game text
- Emulator game translation
- Reading Japanese/Korean game dialogue
- Translating subtitles or Novel text from screen

# Example
### Game Case

- **RPG Game**
  
Translation กับเกม RPG 
<img width="1404" height="789" alt="image" src="https://github.com/user-attachments/assets/81264b85-dc64-414e-b07f-2232a0fefe4a" />
<img width="1402" height="789" alt="image" src="https://github.com/user-attachments/assets/de698eb6-ea37-4a9b-a381-94bae867c88f" />

รับรู้เป้าหมายภารกิจได้ง่ายๆ
<img width="1401" height="788" alt="image" src="https://github.com/user-attachments/assets/8d89e706-89d1-4aa1-b2ac-b8f98e46725d" />
<img width="1400" height="788" alt="image" src="https://github.com/user-attachments/assets/1a90eed1-01bd-47ec-a38e-972d11a4233d" />

เข้าใจบทสนทนาในเกม
<img width="1403" height="787" alt="image" src="https://github.com/user-attachments/assets/88a8bd01-3d69-4591-9f1e-d73925243fa7" />
<img width="1401" height="787" alt="image" src="https://github.com/user-attachments/assets/048de3c2-ec56-4f92-a687-cfc56b17979e" />

- **Visual Novels Game**
  
สืบสวนไปพร้อมกันกับตัวละคร
<img width="1402" height="784" alt="image" src="https://github.com/user-attachments/assets/adbc2503-5833-4c82-a3a3-8c54ba51752f" />
<img width="1402" height="787" alt="image" src="https://github.com/user-attachments/assets/28140eb8-5423-49a9-9150-c394b3fdd22a" />


- **Document**

  อ่านเอกสารได้ง่ายๆ
<img width="1033" height="655" alt="image" src="https://github.com/user-attachments/assets/7e3aa488-28b3-49db-81f7-c165e7ab15d5" />

<img width="1058" height="612" alt="image" src="https://github.com/user-attachments/assets/1197aa42-0b5c-44a2-9ae3-dde2318a14d5" />









# 📥 การติดตั้ง
1) ดาวน์โหลดไฟล์ติดตั้งจากหน้า [Release](https://github.com/PromnarongPomarsa/Angela-Translator/releases/latest) หรือกด [Download](https://github.com/PromnarongPomarsa/Angela-Translator/releases/download/v2.1.1/AngelaTranslator-Setup.exe ) ได้จากตรงนี้
2) เปิดไฟล์ติดตั้งและเลือกตำแหน่ง (Path) ที่ต้องการติดตั้ง
3) หากมีหน้าติดตั้ง Ollama ปรากฏขึ้น ให้ดำเนินการติดตั้ง Ollama (⚠️ **จำเป็น**)
4) เมื่อติดตั้ง Ollama เสร็จแล้ว ให้เข้าสู่ระบบ (Login) Ollama (⚠️ **จำเป็น**)
   <img width="1347" height="787" alt="image" src="https://github.com/user-attachments/assets/b8cca889-72a0-4745-bc39-b35514fef017" />
  <img width="823" height="666" alt="image" src="https://github.com/user-attachments/assets/fc8802ce-e2d2-40e1-9c32-c2eca2eafa0b" />
  <img width="546" height="587" alt="image" src="https://github.com/user-attachments/assets/ba6586b9-609e-48cf-9568-148281b82ae7" />
  
   - แนะนำให้ Login ด้วย Google
  <img width="725" height="578" alt="image" src="https://github.com/user-attachments/assets/da3c3ae4-edf4-4e88-ad5b-b9672e251d8a" />
  
   - กด Connect device เพื่อเข้าใช้งานเป็นอันเสร็จ
6) เมื่อติดตั้ง Angela Translator เสร็จแล้ว สามารถเปิดโปรแกรมและใช้งานได้ทันที
  
⚠️ **หมายเหตุ**
- จำเป็นต้องติดตั้ง Ollama ก่อนใช้งาน Angela Translator
- จำเป็นต้อง Login Ollama ก่อนเปิดใช้งาน Angela Translator ทุกครั้ง

