# tools/generate_combat_model.py
"""Generate a placeholder ONNX model for combat decisions.
Input: 8 floats (distance, selfHP, playerHP, allies, behavior, round, hasMoved, hasAction)
Output: 5 floats (action logits: advance, retreat, flank, attack, hold)

This is a placeholder with random weights. Real training would come later
from Q-table data collected during gameplay.
"""
import numpy as np

try:
    import onnx
    from onnx import helper, TensorProto, numpy_helper
except ImportError:
    print("Installing onnx...")
    import subprocess, sys
    subprocess.check_call([sys.executable, "-m", "pip", "install", "onnx"])
    import onnx
    from onnx import helper, TensorProto, numpy_helper

np.random.seed(42)

# Simple 2-layer network: 8 -> 16 -> 5
W1 = numpy_helper.from_array(np.random.randn(8, 16).astype(np.float32) * 0.5, "W1")
B1 = numpy_helper.from_array(np.zeros(16, dtype=np.float32), "B1")
W2 = numpy_helper.from_array(np.random.randn(16, 5).astype(np.float32) * 0.5, "W2")
B2 = numpy_helper.from_array(np.zeros(5, dtype=np.float32), "B2")

X = helper.make_tensor_value_info("input", TensorProto.FLOAT, [1, 8])
Y = helper.make_tensor_value_info("output", TensorProto.FLOAT, [1, 5])

# Layer 1: matmul + bias + relu
mm1 = helper.make_node("MatMul", ["input", "W1"], ["mm1"])
add1 = helper.make_node("Add", ["mm1", "B1"], ["add1"])
relu1 = helper.make_node("Relu", ["add1"], ["relu1"])
# Layer 2: matmul + bias
mm2 = helper.make_node("MatMul", ["relu1", "W2"], ["mm2"])
add2 = helper.make_node("Add", ["mm2", "B2"], ["output"])

graph = helper.make_graph([mm1, add1, relu1, mm2, add2], "CombatDecision",
                          [X], [Y], [W1, B1, W2, B2])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 17)])
model.ir_version = 8

import os
out_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Assets", "StreamingAssets", "Models")
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, "combat_decision.onnx")
onnx.save(model, out_path)
print(f"Saved combat_decision.onnx ({os.path.getsize(out_path)} bytes)")
