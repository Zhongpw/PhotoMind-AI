using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoMind.Core.Constants;
using PhotoMind.Core.DTOs;
using PhotoMind.Core.Enums;
using PhotoMind.Core.Exceptions;
using PhotoMind.Core.Interfaces;

namespace PhotoMind.App.ViewModels;

/// <summary>Settings page: DeepSeek API key management (DPAPI-backed), test connection, and non-secret AI config.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISecretStore _secretStore;
    private readonly IAiConfigStore _configStore;
    private readonly IDeepSeekClient _client;
    private readonly IClipModelService _clipModel;

    [ObservableProperty] private string _baseUrl = AiConstants.DefaultBaseUrl;
    [ObservableProperty] private string _model = AiConstants.DefaultModel;
    [ObservableProperty] private AiDetailLevel _detail = AiDetailLevel.Low;
    [ObservableProperty] private int _concurrency = AiConstants.DefaultConcurrency;
    [ObservableProperty] private int _maxRetries = AiConstants.DefaultMaxRetries;
    [ObservableProperty] private bool _allowUpload;
    [ObservableProperty] private int _maxPreviewDimension = AiConstants.DefaultMaxPreviewDimension;
    [ObservableProperty] private int _previewQuality = AiConstants.DefaultPreviewQuality;
    [ObservableProperty] private bool _saveTokenMode;
    [ObservableProperty] private bool _gpuAcceleration;

    [ObservableProperty] private string _clipDeviceText = "CLIP 模型未加载（首次分类时自动加载）";
    [ObservableProperty] private bool _isCheckingGpu;

    [ObservableProperty] private string _apiKeyInput = string.Empty;
    [ObservableProperty] private bool _showApiKey;
    [ObservableProperty] private bool _hasApiKey;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private bool? _connectionStatus;
    [ObservableProperty] private bool _isTesting;

    public IReadOnlyList<DetailOption> DetailOptions { get; } = new[]
    {
        new DetailOption(AiDetailLevel.Low, "低（默认，省 token）"),
        new DetailOption(AiDetailLevel.Original, "高（精细识别，耗 token）"),
    };

    public IReadOnlyList<int> ConcurrencyOptions { get; } =
        Enumerable.Range(AiConstants.MinConcurrency, AiConstants.MaxConcurrency - AiConstants.MinConcurrency + 1).ToList();

    public IReadOnlyList<int> MaxRetriesOptions { get; } = Enumerable.Range(0, 11).ToList();

    public IReadOnlyList<int> MaxPreviewDimensionOptions { get; } = new[] { 256, 512, 768, 1024, 1536, 2048 };

    public IReadOnlyList<int> PreviewQualityOptions { get; } = new[] { 60, 70, 75, 80, 85, 90, 95 };

    public SettingsViewModel(ISecretStore secretStore, IAiConfigStore configStore, IDeepSeekClient client, IClipModelService clipModel)
    {
        _secretStore = secretStore;
        _configStore = configStore;
        _client = client;
        _clipModel = clipModel;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var config = await _configStore.LoadAsync().ConfigureAwait(true);
            BaseUrl = config.BaseUrl;
            Model = config.Model;
            Detail = config.Detail;
            Concurrency = config.Concurrency;
            MaxRetries = config.MaxRetries;
            AllowUpload = config.AllowUpload;
            MaxPreviewDimension = config.MaxPreviewDimension;
            PreviewQuality = config.PreviewQuality;
            SaveTokenMode = config.SaveTokenMode;
            GpuAcceleration = config.GpuAcceleration;
            RefreshClipStatus();
        }
        catch (Exception ex)
        {
            StatusText = "加载配置失败：" + ex.Message;
        }

        try
        {
            var kind = await ApiStatusDetector.DetectAsync(_secretStore).ConfigureAwait(true);
            HasApiKey = kind is ApiStatusKind.Configured or ApiStatusKind.Connected or ApiStatusKind.Failed;
        }
        catch (Exception ex)
        {
            StatusText = "读取密钥失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
            await _configStore.SaveAsync(BuildConfig()).ConfigureAwait(true);
            StatusText = "配置已保存";
        }
        catch (Exception ex)
        {
            StatusText = "保存失败：" + ex.Message;
        }
    }

    /// <summary>Builds an <see cref="AiConfig"/> snapshot from the current form values.</summary>
    private AiConfig BuildConfig()
    {
        return new AiConfig
        {
            BaseUrl = BaseUrl.Trim(),
            Model = Model.Trim(),
            Detail = Detail,
            Concurrency = Concurrency,
            MaxRetries = MaxRetries,
            AllowUpload = AllowUpload,
            MaxPreviewDimension = MaxPreviewDimension,
            PreviewQuality = PreviewQuality,
            SaveTokenMode = SaveTokenMode,
            GpuAcceleration = GpuAcceleration,
        };
    }

    /// <summary>Reports the CLIP model's current device/state so the user sees the honest result.</summary>
    private void RefreshClipStatus()
    {
        ClipDeviceText = _clipModel.State switch
        {
            ClipModelState.Ready => _clipModel.Degraded
                ? $"{_clipModel.DeviceDescription}（{_clipModel.DegradeReason}）"
                : _clipModel.DeviceDescription,
            ClipModelState.Failed => "模型加载失败：" + (_clipModel.LastError ?? "未知错误"),
            _ => "CLIP 模型未加载（首次分类时自动加载）",
        };
    }

    [RelayCommand]
    private async Task CheckGpuAsync()
    {
        if (IsCheckingGpu)
            return;

        IsCheckingGpu = true;
        ClipDeviceText = "正在加载 CLIP 模型并检测设备…";
        try
        {
            // Persist the current toggles so device detection uses the latest settings.
            await _configStore.SaveAsync(BuildConfig()).ConfigureAwait(true);
            await _clipModel.EnsureLoadedAsync().ConfigureAwait(true);
            StatusText = "配置已保存";
        }
        catch (Exception ex)
        {
            ClipDeviceText = "模型加载失败：" + ex.Message;
            IsCheckingGpu = false;
            return;
        }

        IsCheckingGpu = false;
        RefreshClipStatus();
    }

    [RelayCommand]
    private async Task SaveApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            StatusText = "请先输入 API Key";
            return;
        }

        try
        {
            await _secretStore.SaveSecretAsync(AiConstants.ApiKeySecretName, ApiKeyInput.Trim()).ConfigureAwait(true);
            ApiKeyInput = string.Empty;
            ShowApiKey = false;
            HasApiKey = true;
            ConnectionStatus = null;
            StatusText = "API Key 已加密保存";
            WeakReferenceMessenger.Default.Send(new ApiStatusChangedMessage(ApiStatusKind.Configured));
        }
        catch (Exception ex)
        {
            StatusText = "保存失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteApiKeyAsync()
    {
        try
        {
            await _secretStore.DeleteSecretAsync(AiConstants.ApiKeySecretName).ConfigureAwait(true);
            ApiKeyInput = string.Empty;
            HasApiKey = false;
            ConnectionStatus = null;
            StatusText = "API Key 已删除";
            WeakReferenceMessenger.Default.Send(new ApiStatusChangedMessage(ApiStatusKind.NotConfigured));
        }
        catch (Exception ex)
        {
            StatusText = "删除失败：" + ex.Message;
        }
    }

    [RelayCommand]
    private void ToggleShowKey() => ShowApiKey = !ShowApiKey;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        ConnectionStatus = null;
        StatusText = "正在测试连接…";

        try
        {
            var key = await _secretStore.GetSecretAsync(AiConstants.ApiKeySecretName).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(key))
                key = ApiKeyInput.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                StatusText = "请先填写并保存 API Key";
                return;
            }

            var baseUrl = BaseUrl.Trim().TrimEnd('/');
            var request = new DeepSeekRequest
            {
                Model = Model.Trim(),
                Messages = new List<DeepSeekMessage>
                {
                    new DeepSeekMessage { Role = "user", Content = "请只回复两个字：正常" },
                },
            };

            await _client.ChatAsync(baseUrl, key, request).ConfigureAwait(true);

            ConnectionStatus = true;
            StatusText = "连接成功，API Key 有效";
            WeakReferenceMessenger.Default.Send(new ApiStatusChangedMessage(ApiStatusKind.Connected));
        }
        catch (DeepSeekException ex)
        {
            ConnectionStatus = false;
            StatusText = "连接失败：" + ex.Message;
            WeakReferenceMessenger.Default.Send(new ApiStatusChangedMessage(ApiStatusKind.Failed));
        }
        catch (Exception ex)
        {
            ConnectionStatus = false;
            StatusText = "连接失败：" + ex.Message;
            WeakReferenceMessenger.Default.Send(new ApiStatusChangedMessage(ApiStatusKind.Failed));
        }
        finally
        {
            IsTesting = false;
        }
    }
}

/// <summary>ComboBox item for the vision detail level.</summary>
public sealed class DetailOption
{
    public DetailOption(AiDetailLevel value, string label)
    {
        Value = value;
        Label = label;
    }

    public AiDetailLevel Value { get; }
    public string Label { get; }
    public override string ToString() => Label;
}
