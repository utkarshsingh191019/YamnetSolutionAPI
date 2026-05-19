import tensorflow as tf # 2.19.0
import tensorflow_hub as hub # 0.16.1
import librosa # 0.9.2
import numpy as np
import pandas as pd
import os
import sys
import json
import re
import multiprocessing

# Globals to hold model and class map per worker
model = None
class_names = None

print("Loading YAMNet model and class map...", file=sys.stderr)

# base_path = os.path.dirname(os.path.abspath(__file__))
# ten_flow_path = os.path.join(base_path,"Yamnet Model")
# model = tf.saved_model.load(ten_flow_path)
# class_map_path = os.path.join(base_path,"Yamnet Model","assets","yamnet_class_map.csv")
# class_names = pd.read_csv(class_map_path)['display_name'].to_list()

def init_worker(model_dir):
    global model, class_names
    base_path = os.path.dirname(os.path.abspath(__file__))
    ten_flow_path = os.path.join(base_path,"Yamnet Model")
    model = tf.saved_model.load(ten_flow_path)
    class_map_path = os.path.join(ten_flow_path, "assets", "yamnet_class_map.csv")
    class_names = pd.read_csv(class_map_path)['display_name'].to_list()
def natural_sort_key(text):
    return [int(s) if s.isdigit() else s.lower() for s in re.split(r'(\d+)', text)]

def analyze_chunk(file_path):
    global model, class_names
    result = {
        "ChunkPath": file_path,
        "Labels": [],
        "Scores": [],
        "Timestamps": []
    }
    if not os.path.exists(file_path):
        result["Error"] = f"File not found: {file_path}"
        return result
    try:
        y, sr = librosa.load(file_path, sr=None)
        y_16k = librosa.resample(y, orig_sr=sr, target_sr=16000)
        waveform = tf.convert_to_tensor(y_16k, dtype=tf.float32)
        scores, _, _ = model(waveform)
        frame_duration = 0.96
        timestamps = np.arange(scores.shape[0]) * frame_duration
        true_duration = len(y_16k) / 16000
        for i, t in enumerate(timestamps):
            if t > true_duration:
                break
            top_idx = tf.argmax(scores[i]).numpy()
            label = class_names[top_idx]
            confidence = scores[i][top_idx].numpy()
            result["Labels"].append(label)
            result["Scores"].append(round(float(confidence), 4))
            result["Timestamps"].append(round(float(t), 2))
    except Exception as e:
        result["Error"] = str(e)
    return result

if __name__ == "__main__":
    base_path = os.path.dirname(os.path.abspath(__file__))
    audio_dir = os.path.join(base_path, "Audio", "audio_chunks")
    model_dir = os.path.join(base_path, "Yamnet Model")
    if not os.path.exists(audio_dir):
        print(json.dumps({"error": f"AudioChunks folder not found at: {audio_dir}"}))
        sys.exit(1)
    chunk_paths = sorted(
        [os.path.join(audio_dir, f)
         for f in os.listdir(audio_dir)
         if f.lower().endswith(".wav")],
        key=lambda x: natural_sort_key(os.path.basename(x))
    )
    if not chunk_paths:
        print(json.dumps({"error": "No .wav files found in AudioChunks folder."}))
        sys.exit(1)
    print(f"Found {len(chunk_paths)} audio chunks to analyze...", file=sys.stderr)
    num_processes = max(1, multiprocessing.cpu_count() - 1)
    with multiprocessing.Pool(
        processes=num_processes,
        initializer=init_worker,
        initargs=(model_dir,)
    ) as pool:
        all_results = pool.map(analyze_chunk, chunk_paths)
    print(json.dumps(all_results, indent=2))
    
