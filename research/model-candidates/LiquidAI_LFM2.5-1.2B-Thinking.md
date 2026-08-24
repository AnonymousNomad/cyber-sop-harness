---
library_name: transformers
license: other
license_name: lfm1.0
license_link: LICENSE
language:
- en
- ar
- zh
- fr
- de
- ja
- ko
- es
pipeline_tag: text-generation
tags:
- liquid
- lfm2.5
- edge
base_model: LiquidAI/LFM2.5-1.2B-Base
---

<div align="center">
  <img 
    src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/2b08LKpev0DNEk6DlnWkY.png" 
    alt="Liquid AI" 
    style="width: 100%; max-width: 100%; height: auto; display: inline-block; margin-bottom: 0.5em; margin-top: 0.5em;"
  />
  <div style="display: flex; justify-content: center; gap: 0.5em;">
<a href="https://playground.liquid.ai/"><strong>Try LFM</strong></a> • <a href="https://docs.liquid.ai/lfm/getting-started/welcome"><strong>Docs</strong></a> • <a href="https://leap.liquid.ai/"><strong>LEAP</strong></a> • <a href="https://discord.com/invite/liquid-ai"><strong>Discord</strong></a>
  </div>
</div>

<br>

# LFM2.5-1.2B-Thinking

LFM2.5 is a new family of hybrid models designed for **on-device deployment**. It builds on the LFM2 architecture with extended pre-training and reinforcement learning.

- **Best-in-class performance**: A 1.2B model rivaling much larger models, bringing high-quality AI to your pocket.
- **Fast edge inference**: 239 tok/s decode on AMD CPU, 82 tok/s on mobile NPU. Runs under 1GB of memory with day-one support for llama.cpp, MLX, and vLLM.
- **Scaled training**: Extended pre-training from 10T to 28T tokens and large-scale multi-stage reinforcement learning.

![LFM2.5-1.2B - Benchmarks-Light](https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/KfNudLXnOZxAhlLp_1QVo.png)

Find more information about LFM2.5-1.2B-Thinking in our [blog post](https://www.liquid.ai/blog/lfm2-5-1-2b-thinking-on-device-reasoning-under-1gb).

## 🗒️ Model Details

| Model | Parameters | Description |
|-------|------------|-------------|
| [LFM2.5-1.2B-Base](https://huggingface.co/LiquidAI/LFM2.5-1.2B-Base) | 1.2B | Pre-trained base model for fine-tuning |
| [LFM2.5-1.2B-Instruct](https://huggingface.co/LiquidAI/LFM2.5-1.2B-Instruct) | 1.2B | General-purpose instruction-tuned model |
| [**LFM2.5-1.2B-Thinking**](https://huggingface.co/LiquidAI/LFM2.5-1.2B-Thinking) | 1.2B | General-purpose reasoning model |
| [LFM2.5-1.2B-JP](https://huggingface.co/LiquidAI/LFM2.5-1.2B-JP) | 1.2B | Japanese-optimized chat model |
| [LFM2.5-VL-1.6B](https://huggingface.co/LiquidAI/LFM2.5-VL-1.6B) | 1.6B | Vision-language model with fast inference |
| [LFM2.5-Audio-1.5B](https://huggingface.co/LiquidAI/LFM2.5-Audio-1.5B) | 1.5B | Audio-language model for speech and text I/O |

LFM2.5-1.2B-Thinking is a general-purpose text-only model with the following features:

- **Number of parameters**: 1.17B
- **Number of layers**: 16 (10 double-gated convolution blocks + 6 GQA blocks)
- **Training budget**: 28T tokens
- **Context length**: 32,768 tokens
- **Vocabulary size**: 65,536
- **Knowledge cutoff**: Mid-2024
- **Languages**: English, Arabic, Chinese, French, German, Japanese, Korean, Spanish
- **Generation parameters**:
  - `temperature: 0.05`
  - `top_k: 50`
  - `repetition_penalty: 1.05`

| Model | Description |
|-------|-------------|
| [**LFM2.5-1.2B-Thinking**](https://huggingface.co/LiquidAI/LFM2.5-1.2B-Thinking) | Original model checkpoint in native format. Best for fine-tuning or inference with Transformers and vLLM. |
| [LFM2.5-1.2B-Thinking-GGUF](https://huggingface.co/LiquidAI/LFM2.5-1.2B-Thinking-GGUF) | Quantized format for llama.cpp and compatible tools. Optimized for CPU inference and local deployment with reduced memory usage. |
| [LFM2.5-1.2B-Thinking-ONNX](https://huggingface.co/LiquidAI/LFM2.5-1.2B-Thinking-ONNX) | ONNX Runtime format for cross-platform deployment. Enables hardware-accelerated inference across diverse environments (cloud, edge, mobile). |
| [LFM2.5-1.2B-Thinking-MLX](https://huggingface.co/LiquidAI/LFM2.5-1.2B-Thinking-MLX-8bit) | MLX format for Apple Silicon. Optimized for fast inference on Mac devices using the MLX framework. |

We recommend using it for agentic tasks, data extraction, and RAG. It is not recommended for knowledge-intensive tasks and programming.

### Chat Template

LFM2.5 uses a ChatML-like format. See the [Chat Template documentation](https://docs.liquid.ai/lfm/key-concepts/chat-template) for details. Example:

```
<|startoftext|><|im_start|>system
You are a helpful assistant trained by Liquid AI.<|im_end|>
<|im_start|>user
What is C. elegans?<|im_end|>
<|im_start|>assistant
```

You can use [`tokenizer.apply_chat_template()`](https://huggingface.co/docs/transformers/en/chat_templating#using-applychattemplate) to format your messages automatically.

### Tool Use

LFM2.5 supports function calling as follows:

1. **Function definition**: We recommend providing the list of tools as a JSON object in the system prompt. You can also use the [`tokenizer.apply_chat_template()`](https://huggingface.co/docs/transformers/en/chat_extras#passing-tools) function with tools.
2. **Function call**: By default, LFM2.5 writes Pythonic function calls (a Python list between `<|tool_call_start|>` and `<|tool_call_end|>` special tokens), as the assistant answer. You can override this behavior by asking the model to output JSON function calls in the system prompt.
3. **Function execution**: The function call is executed, and the result is returned as a "tool" role.
4. **Final answer**: LFM2 interprets the outcome of the function call to address the original user prompt in plain text.

See the [Tool Use documentation](https://docs.liquid.ai/lfm/key-concepts/tool-use) for the full guide. Example:

```
<|startoftext|><|im_start|>system
List of tools: [{"name": "get_candidate_status", "description": "Retrieves the current status of a candidate in the recruitment process", "parameters": {"type": "object", "properties": {"candidate_id": {"type": "string", "description": "Unique identifier for the candidate"}}, "required": ["candidate_id"]}}]<|im_end|>
<|im_start|>user
What is the current status of candidate ID 12345?<|im_end|>
<|im_start|>assistant
<|tool_call_start|>[get_candidate_status(candidate_id="12345")]<|tool_call_end|>Checking the current status of candidate ID 12345.<|im_end|>
<|im_start|>tool
[{"candidate_id": "12345", "status": "Interview Scheduled", "position": "Clinical Research Associate", "date": "2023-11-20"}]<|im_end|>
<|im_start|>assistant
The candidate with ID 12345 is currently in the "Interview Scheduled" stage for the position of Clinical Research Associate, with an interview date set for 2023-11-20.<|im_end|>
```

## 🏃 Inference

LFM2.5 is supported by many inference frameworks. See the [Inference documentation](https://docs.liquid.ai/lfm/inference/transformers) for the full list.

| Name | Description | Docs | Notebook |
|------|-------------|------|:--------:|
| [Transformers](https://github.com/huggingface/transformers) | Simple inference with direct access to model internals. | <a href="https://docs.liquid.ai/lfm/inference/transformers">Link</a> | <a href="https://colab.research.google.com/drive/1_q3jQ6LtyiuPzFZv7Vw8xSfPU5FwkKZY?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| [vLLM](https://github.com/vllm-project/vllm) | High-throughput production deployments with GPU. | <a href="https://docs.liquid.ai/lfm/inference/vllm">Link</a> | <a href="https://colab.research.google.com/drive/1VfyscuHP8A3we_YpnzuabYJzr5ju0Mit?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| [llama.cpp](https://github.com/ggml-org/llama.cpp) | Cross-platform inference with CPU offloading. | <a href="https://docs.liquid.ai/lfm/inference/llama-cpp">Link</a> | <a href="https://colab.research.google.com/drive/1ohLl3w47OQZA4ELo46i5E4Z6oGWBAyo8?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| [MLX](https://github.com/ml-explore/mlx) | Apple's machine learning framework optimized for Apple Silicon. | <a href="https://docs.liquid.ai/lfm/inference/mlx">Link</a> | — |
| [LM Studio](https://lmstudio.ai/) | Desktop application for running LLMs locally. | <a href="https://docs.liquid.ai/lfm/inference/lm-studio">Link</a> | — |

Here's a quick start example with Transformers:

```python
from transformers import AutoModelForCausalLM, AutoTokenizer, TextStreamer

model_id = "LiquidAI/LFM2.5-1.2B-Thinking"
model = AutoModelForCausalLM.from_pretrained(
    model_id,
    device_map="auto",
    dtype="bfloat16",
#   attn_implementation="flash_attention_2" <- uncomment on compatible GPU
)
tokenizer = AutoTokenizer.from_pretrained(model_id)
streamer = TextStreamer(tokenizer, skip_prompt=True, skip_special_tokens=True)

prompt = "What is C. elegans?"

input_ids = tokenizer.apply_chat_template(
    [{"role": "user", "content": prompt}],
    add_generation_prompt=True,
    return_tensors="pt",
    tokenize=True,
).to(model.device)

output = model.generate(
    input_ids,
    do_sample=True,
    temperature=0.05,
    top_k=50,
    repetition_penalty=1.05,
    max_new_tokens=512,
    streamer=streamer,
)
```

## 🔧 Fine-Tuning

We recommend fine-tuning LFM2.5 for your specific use case to achieve the best results.

| Name | Description | Docs | Notebook |
|------|-------------|------|----------|
| CPT ([Unsloth](https://github.com/unslothai/unsloth)) | Continued Pre-Training using Unsloth for text completion. | <a href="https://docs.liquid.ai/lfm/fine-tuning/unsloth">Link</a> | <a href="https://colab.research.google.com/drive/10fm7eNMezs-DSn36mF7vAsNYlOsx9YZO?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| CPT ([Unsloth](https://github.com/unslothai/unsloth)) | Continued Pre-Training using Unsloth for translation. | <a href="https://docs.liquid.ai/lfm/fine-tuning/unsloth">Link</a> | <a href="https://colab.research.google.com/drive/1gaP8yTle2_v35Um8Gpu9239fqbU7UgY8?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| SFT ([Unsloth](https://github.com/unslothai/unsloth)) | Supervised Fine-Tuning with LoRA using Unsloth. | <a href="https://docs.liquid.ai/lfm/fine-tuning/unsloth">Link</a> | <a href="https://colab.research.google.com/drive/1vGRg4ksRj__6OLvXkHhvji_Pamv801Ss?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| SFT ([TRL](https://github.com/huggingface/trl)) | Supervised Fine-Tuning with LoRA using TRL. | <a href="https://docs.liquid.ai/lfm/fine-tuning/trl">Link</a> | <a href="https://colab.research.google.com/drive/1j5Hk_SyBb2soUsuhU0eIEA9GwLNRnElF?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| DPO ([TRL](https://github.com/huggingface/trl)) | Direct Preference Optimization with LoRA using TRL. | <a href="https://docs.liquid.ai/lfm/fine-tuning/trl">Link</a> | <a href="https://colab.research.google.com/drive/1MQdsPxFHeZweGsNx4RH7Ia8lG8PiGE1t?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| GRPO ([Unsloth](https://github.com/unslothai/unsloth)) | GRPO with LoRA using Unsloth. | <a href="https://docs.liquid.ai/lfm/fine-tuning/unsloth">Link</a> | <a href="https://colab.research.google.com/drive/1mIikXFaGvcW4vXOZXLbVTxfBRw_XsXa5?usp=sharing"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |
| GRPO ([TRL](https://github.com/huggingface/trl)) | GRPO with LoRA using TRL. | <a href="https://docs.liquid.ai/lfm/fine-tuning/trl">Link</a> | <a href="https://colab.research.google.com/github/Liquid4All/cookbook/blob/main/finetuning/notebooks/grpo_for_verifiable_tasks.ipynb"><img src="https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/vlOyMEjwHa_b_LXysEu2E.png" width="110" alt="Colab link"></a> |

## 📊 Performance

### Benchmarks

We compared LFM2.5-1.2B-Thinking with relevant sub-2B models on a diverse suite of benchmarks.

| Model                      | GPQA Diamond      | MMLU-Pro          | IFEval            | IFBench           | Multi-IF          | GSM8K             | MATH-500          | AIME25            | BFCLv3            |
| -------------------------- | ----------------- | ----------------- | ----------------- | ----------------- | ----------------- | ----------------- | ----------------- | ----------------- | ----------------- |
| **LFM2.5-1.2B-Thinking**   | 37.86<br>(± 0.83) | 49.65<br>(± 0.18) | 88.42<br>(± 0.35) | 44.85<br>(± 0.73) | 69.33<br>(± 0.09) | 85.60<br>(± 0.00) | 87.96<br>(± 0.72) | 31.73<br>(± 1.81) | 56.97<br>(± 0.30) |
| Qwen3-1.7B (thinking mode) | 36.93<br>(± 2.07) | 56.68<br>(± 1.29) | 71.65<br>(± 0.13) | 25.88<br>(± 0.30) | 60.33<br>(± 0.02) | 85.60<br>(± 1.13) | 81.92<br>(± 2.99) | 36.27<br>(± 1.24) | 55.41<br>(± 0.04) |
| LFM2.5-1.2B-Instruct       | 38.89             | 44.35             | 86.23             | 47.33             | 60.98             | 64.52             | 63.20             | 14.00             | 49.12             |
| Qwen3-1.7B (instruct mode) | 34.85             | 42.91             | 73.68             | 21.33             | 56.48             | 33.66             | 70.40             | 9.33              | 46.30             |
| Granite-4.0-H-1B           | 24.34             | 27.64             | 80.08             | 24.93             | 47.56             | 69.60             | 47.20             | 1                 | 50.69             |
| Granite-4.0-1B             | 24.24             | 33.53             | 79.61             | 21                | 43.65             | 73.42             | 44.80             | 3.33              | 52.43             |
| Gemma 3 1B IT              | 24.24             | 14.04             | 63.25             | 20.47             | 44.31             | 42.15             | 45.20             | 1                 | 16.64             |
| Llama 3.2 1B Instruct      | 16.57             | 20.80             | 52.37             | 15.93             | 30.16             | 39.04             | 23.40             | 0.33              | 21.44             |

GPQA, MMLU-Pro, IFBench, and AIME25 follow [ArtificialAnalysis's methodology](https://artificialanalysis.ai/methodology/intelligence-benchmarking). For IFEval and Multi-IF, we report the average score across strict and loose prompt and instruction accuracies. For BFCLv3, we report the final weighted average score with a custom Liquid handler to support our tool use template. 

Based on the same methodology, we report the average score and standard deviation across five runs with `temperature=0.6` for thinking models. For instruct models, we report scores using greedy decoding.

### Response length

In comparison with Qwen3-1.7B (thinking mode), it requires fewer output tokens while offering higher overall performance.

![LFM2.5-1.2B - Average Response Length](https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/Gcq_HUYLVC779xOuut2EI.png)

### Inference speed

LFM2.5-1.2B-Thinking offers extremely fast inference speed on CPUs with a low memory profile compared to similar-sized models.

![LFM2.5-1.2B - Inference Performance](https://cdn-uploads.huggingface.co/production/uploads/61b8e2ba285851687028d395/4ODY8nGws22vICfcMTxNx.png)

In addition, we are partnering with AMD, Qualcomm, Nexa AI, and FastFlowLM to bring the LFM2.5 family to NPUs. These optimized models are available through our partners, enabling highly efficient on-device inference.

We report the following numbers with 1K prefill and 100 decode tokens: 

| Device                                               | Inference | Framework        | Model                | Prefill (tok/s) | Decode (tok/s) | Memory |
| ---------------------------------------------------- | --------- | ---------------- | -------------------- | --------------- | -------------- | ------ |
| AMD Ryzen AI 395+                                    | NPU       | FastFlowLM       | LFM2.5-1.2B-Thinking | 1487            | 60             | 1600MB (full context) |
| AMD Ryzen AI 9 HX 370                                | NPU       | FastFlowLM       | LFM2.5-1.2B-Thinking | 1487            | 57             | 1600MB (full context) |
| AMD Ryzen AI 9 HX 370                                | CPU       | llama.cpp (Q4_0) | LFM2.5-1.2B-Thinking | 2975            | 116            | 856MB  |
| Qualcomm Snapdragon® X Elite                         | NPU       | NexaML           | LFM2.5-1.2B-Thinking | 2591            | 63             | 0.9GB  |
| Qualcomm Snapdragon® Gen4 (ROG Phone9 Pro)           | NPU       | NexaML           | LFM2.5-1.2B-Thinking | 4391            | 82             | 0.9GB  |
| Qualcomm Dragonwing IQ9 (IQ-9075) (IoT)              | NPU       | NexaML           | LFM2.5-1.2B-Thinking | 2143            | 53             | 0.9 GB |
| Qualcomm Snapdragon® Gen4 (Samsung Galaxy S25 Ultra) | CPU       | llama.cpp (Q4_0) | LFM2.5-1.2B-Thinking | 335             | 70             | 719MB  |

**LFM2.5-1.2B-Thinking excels at long-context inference.** For example, on AMD Ryzen™ NPUs with FastFlowLM, decoding throughput sustains ~52 tok/s at 16K context and ~46 tok/s even at the full 32K context, indicating robust long-context scalability. For more details on longer context benchmarks on AMD Ryzen™ NPUs with FastFlowLM, please review these [here](https://fastflowlm.com/docs/benchmarks/lfm2_results/).

These capabilities unlock new deployment scenarios across various devices, including vehicles, mobile devices, laptops, IoT devices, and embedded systems.

## 📬 Contact

- Got questions or want to connect? [Join our Discord community](https://discord.com/invite/liquid-ai)
- If you are interested in custom solutions with edge deployment, please contact [our sales team](https://www.liquid.ai/contact).

## Citation

```bibtex
@article{liquidAI2026thinking,
  author = {Liquid AI},
  title = {LFM2.5-1.2B-Thinking: On-Device Reasoning Under 1GB},
  journal = {Liquid AI Blog},
  year = {2026},
  note = {www.liquid.ai/blog/lfm2-5-1-2b-thinking-on-device-reasoning-under-1gb},
}
```

```bibtex
@article{liquidai2025lfm2,
  title={LFM2 Technical Report},
  author={Liquid AI},
  journal={arXiv preprint arXiv:2511.23404},
  year={2025}
}
```