using Microsoft.ML.OnnxRuntime;

var clipDir = args.Length > 0 ? args[0] : @"D:\AI_Agent+\dsh_next\PhotoMindAI\.models\clip";

foreach (var model in new[] { "vision_model.onnx", "text_model.onnx" })
{
    var path = Path.Combine(clipDir, model);
    Console.WriteLine($"===== {model} =====");
    using var session = new InferenceSession(path);
    foreach (var kv in session.InputMetadata)
        Console.WriteLine($"  IN  '{kv.Key}'  type={kv.Value.ElementType} dims=[{string.Join(",", kv.Value.Dimensions)}]");
    foreach (var kv in session.OutputMetadata)
        Console.WriteLine($"  OUT '{kv.Key}'  type={kv.Value.ElementType} dims=[{string.Join(",", kv.Value.Dimensions)}]");
    session.Dispose();
}
