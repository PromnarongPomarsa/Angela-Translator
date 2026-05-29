# Angela-Translator
โปรแกรมแปลข้อความจากหน้าจอแบบ Selection Translate ที่ออกแบบมาสำหรับเกม Visual Novel, RPG, Emulator หรือสื่อบันเทิงต่าง ๆ โดยผู้ใช้สามารถเลือกเฉพาะพื้นที่บนหน้าจอที่ต้องการแปลได้โดยตรง

โปรแกรมจะจับข้อความจากพื้นที่ที่เลือกด้วย OCR แล้วนำข้อความทั้งหมดในพื้นที่นั้นมารวมและเรียงต่อกันเป็นประโยคก่อนทำการแปล เพื่อให้ได้ผลลัพธ์ที่เหมาะกับบทสนทนาในเกมหรือข้อความที่ถูกแบ่งหลายบรรทัดบนหน้าจอ

### คำศัพท์เฉพาะ
- OCR คือ โปรแกรมที่ใช้อ่านข้อความจากรูปภาพ
- Ollama คือ โปรแกรม Runtime ที่ใช้สำหรับ Run AI ทั้งในรูปแบบ Local หรือ Cloud 

### รองรับการแปล Dialog เกมแบบต่อเนื่อง

- เลือกพื้นที่ Subtitle หรือ Dialog เพียงครั้งเดียว
- กดแปลต่อเนื่องได้ทันที
- ไม่ต้องลากเลือกพื้นที่ใหม่ทุกประโยค
- เหมาะสำหรับเกม Visual Novel และ RPG

# Important Notes

โปรแกรมนี้ถูกออกแบบมาสำหรับการแปลข้อความเฉพาะส่วนของหน้าจอ เน้นใช้ในการแปลเป็นประโยคหรือ Sentence จึงมีข้อแนะนำดังนี้

เพื่อประสิทธิภาพที่ดีที่สุด:

- ควรเลือกเฉพาะพื้นที่ข้อความที่ต้องการแปล
- หากใช้อ่านเอกสาร (Document / PDF / Website) ควรเลือกแปลทีละ Paragraph
- การเลือกพื้นที่ขนาดใหญ่เกินไปอาจทำให้ OCR และการแปลมีความแม่นยำน้อยลง

# Hotkeys
- **Select Screen Area:**        Alt + S 
- **Translate Selected Area:**   Alt + D
- **Exit or Cencel translation:** ESC

# Features
- Selection Translate แบบเลือกพื้นที่บนหน้าจอ
- OCR จับข้อความจากหน้าจอด้วย PaddleOCR
- แปลข้อความด้วย AI Models ผ่าน Ollama
- รวมข้อความหลายบรรทัดก่อนแปลเพื่อให้ประโยคสมบูรณ์
- เหมาะสำหรับเกม Visual Novel, RPG, Emulator และสื่อบันเทิง
- รองรับการใช้งานแบบ Hotkey
- หากใช้กับเกม Dialog subtitle ให้เลือกพื้นที่ Selection เพียงแค่ครั้งเดียวและกดแปลได้เลยตลอด ไม่ต้องเลือกใหม่

# How It Works
- ผู้ใช้เลือกพื้นที่บนหน้าจอ
- โปรแกรมจับภาพเฉพาะบริเวณที่เลือก
- ใช้ PaddleOCR ตรวจจับข้อความ
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
Before translate
<img width="1270" height="237" alt="image" src="https://github.com/user-attachments/assets/34f62ec7-6f33-4bbe-8b06-90bfd69215c7" />
After translated
<img width="1260" height="379" alt="image" src="https://github.com/user-attachments/assets/396bcf24-2e7f-4756-be1c-e1a6d2da5ead" />

