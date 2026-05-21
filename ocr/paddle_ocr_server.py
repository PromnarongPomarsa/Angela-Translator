import os

os.environ["PADDLEX_DISABLE_DEPS_CHECK"] = "1"
os.environ["DISABLE_MODEL_SOURCE_CHECK"] = "1"
os.environ["PADDLE_PDX_EAGER_INIT"] = "0"

from flask import Flask, request, jsonify
from paddleocr import PaddleOCR
import base64
import cv2
import numpy as np
import json
import threading
import time

app = Flask(__name__)

OCR_LOCK = threading.Lock()
MAX_INPUT_SIDE = 1280
DET_LIMIT_SIDE_LEN = 1080
MIN_SCORE = 0.45

ocr_cache = {}

def get_ocr(lang: str) -> PaddleOCR:
    if lang not in ocr_cache:
        print(f"[ocr] Loading model lang={lang}")
        ocr_cache[lang] = PaddleOCR(
            lang=lang,
            use_angle_cls=False,
            use_gpu=False,
            det_limit_side_len=DET_LIMIT_SIDE_LEN,
            det_limit_type="max",
            det_db_thresh=0.3,
            det_db_box_thresh=0.5,
            rec_batch_num=6,
            enable_mkldnn=True,
            cpu_threads=6,
            show_log=False,
        )
    return ocr_cache[lang]



def decode_image(image_b64: str):
    image_bytes = base64.b64decode(image_b64)
    np_arr = np.frombuffer(image_bytes, np.uint8)
    return cv2.imdecode(np_arr, cv2.IMREAD_COLOR)


def auto_crop_text_region(image: np.ndarray):
    h, w = image.shape[:2]
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    _, mask_light = cv2.threshold(gray, 180, 255, cv2.THRESH_BINARY)      # ข้อความสว่างบน bg มืด
    _, mask_dark = cv2.threshold(gray, 80, 255, cv2.THRESH_BINARY_INV)    # ข้อความมืดบน bg สว่าง
    mask = cv2.bitwise_or(mask_light, mask_dark)

    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (25, 7))
    merged = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel, iterations=2)
    contours, _ = cv2.findContours(merged, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    boxes = []
    for cnt in contours:
        x, y, bw, bh = cv2.boundingRect(cnt)
        if bw < 40 or bh < 12:
            continue
        boxes.append((x, y, bw, bh))

    if not boxes:
        return image, 0, 0

    x1 = min(x for x, y, bw, bh in boxes)
    y1 = min(y for x, y, bw, bh in boxes)
    x2 = max(x + bw for x, y, bw, bh in boxes)
    y2 = max(y + bh for x, y, bw, bh in boxes)

    pad_x, pad_y = 24, 20
    x1 = max(0, x1 - pad_x)
    y1 = max(0, y1 - pad_y)
    x2 = min(w, x2 + pad_x)
    y2 = min(h, y2 + pad_y)

    return image[y1:y2, x1:x2].copy(), x1, y1


def resize_if_needed(image: np.ndarray, max_side: int = MAX_INPUT_SIDE):
    h, w = image.shape[:2]
    longest = max(h, w)
    if longest <= max_side:
        return image, 1.0
    scale = max_side / float(longest)
    new_w = max(1, int(w * scale))
    new_h = max(1, int(h * scale))
    return cv2.resize(image, (new_w, new_h), interpolation=cv2.INTER_AREA), scale


def poly_to_rect(poly, scale: float, offset_x: int, offset_y: int):
    pts = np.array(poly, dtype=np.float32).reshape(-1, 2)
    if scale > 0:
        pts /= scale
    xs = pts[:, 0].tolist()
    ys = pts[:, 1].tolist()
    return {
        "x": int(min(xs)) + offset_x,
        "y": int(min(ys)) + offset_y,
        "w": int(max(xs) - min(xs)),
        "h": int(max(ys) - min(ys)),
    }


def run_ocr(image: np.ndarray, scale: float, offset_x: int, offset_y: int, lang: str = "japan"):
    ocr = get_ocr(lang)  # โหลด model ตาม lang
    with OCR_LOCK:
        result = ocr.ocr(image, cls=False)

    words = []
    if not result or result[0] is None:
        return words

    for line in result[0]:
        if not line:
            continue
        poly = line[0]
        text = (line[1][0] or "").strip()
        score = float(line[1][1])

        if not text or score < MIN_SCORE:
            continue

        words.append({
            "text": text,
            "confidence": score,
            "box": poly_to_rect(poly, scale, offset_x, offset_y),
        })

    return words


def warm_up():
    dummy = np.full((96, 320, 3), 0, dtype=np.uint8)
    cv2.putText(dummy, "test", (20, 60), cv2.FONT_HERSHEY_SIMPLEX, 1.5, (255, 255, 255), 3)
    try:
        ocr_ja.ocr(dummy, cls=False)
        print("[warmup] ok")
    except Exception as ex:
        print(f"[warmup] failed: {ex}")


@app.get("/health")
def health():
    return {"ok": True}


@app.post("/ocr")
def ocr():
    started = time.perf_counter()

    data = request.get_json(force=True)
    if isinstance(data, str):
        data = json.loads(data)
    if not isinstance(data, dict):
        return jsonify({"error": "invalid json payload"}), 400

    image_b64 = data.get("image", "")
    if not image_b64:
        return jsonify({"error": "image is required"}), 400

    image = decode_image(image_b64)
    if image is None:
        return jsonify({"error": "invalid image"}), 400

    # cropped, offset_x, offset_y = auto_crop_text_region(image)
    cropped, offset_x, offset_y = image, 0, 0  # ใช้ภาพทั้งหมด 
    resized, scale = resize_if_needed(cropped, MAX_INPUT_SIDE)
    lang = data.get("lang", "japan")

    words = run_ocr(resized, scale, offset_x, offset_y, lang)

    elapsed_ms = int((time.perf_counter() - started) * 1000)
    print(f"[ocr] total={elapsed_ms}ms words={len(words)}")

    return jsonify({
        "elapsedMs": elapsed_ms,
        "crop": {
            "x": offset_x,
            "y": offset_y,
            "w": int(cropped.shape[1]),
            "h": int(cropped.shape[0]),
        },
        "words": words,
    })


if __name__ == "__main__":
    warm_up()
    port = int(os.environ.get("PORT", 5000))
    app.run(host="0.0.0.0", port=port, threaded=True)