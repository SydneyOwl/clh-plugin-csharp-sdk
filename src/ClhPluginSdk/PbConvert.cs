using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto = SydneyOwl.CLHProto.Plugin;

namespace ClhPluginSdk;

internal readonly record struct InboundConversionResult(IMessage? ProtoMessage, Message ModelMessage);

internal static class PbConvert
{
    public static Timestamp NowTimestamp()
    {
        return Timestamp.FromDateTime(DateTime.UtcNow);
    }

    public static Proto.PipeEventSubscription? ToPbEventSubscription(EventSubscription? input)
    {
        if (input is null)
        {
            return null;
        }

        var output = new Proto.PipeEventSubscription();
        foreach (var topic in input.Topics)
        {
            output.Topics.Add((Proto.PipeEnvelopeTopic)topic);
        }

        return output;
    }

    public static EventSubscription FromPbEventSubscription(Proto.PipeEventSubscription? input)
    {
        var output = new EventSubscription();
        if (input is null)
        {
            return output;
        }

        foreach (var topic in input.Topics)
        {
            output.Topics.Add((EnvelopeTopic)topic);
        }

        return output;
    }

    public static Proto.PipeNotificationCommand ToPbNotification(NotificationCommand input)
    {
        return new Proto.PipeNotificationCommand
        {
            Level = (Proto.PipeNotificationLevel)input.Level,
            Title = input.Title,
            Message = input.Message
        };
    }

    public static Proto.PipeSettingsPatch ToPbSettingsPatch(SettingsPatch input)
    {
        var output = new Proto.PipeSettingsPatch();
        foreach (var item in input.Values)
        {
            output.Values[item.Key] = item.Value;
        }

        return output;
    }

    public static Proto.PipeRegisterPluginReq ToPbManifest(PluginManifest input)
    {
        var output = new Proto.PipeRegisterPluginReq
        {
            Uuid = input.Uuid,
            Name = input.Name,
            Version = input.Version,
            Description = input.Description,
            SdkName = input.SdkName,
            SdkVersion = input.SdkVersion,
            EventSubscription = ToPbEventSubscription(input.EventSubscription),
            Timestamp = NowTimestamp()
        };

        foreach (var item in input.Metadata)
        {
            output.Metadata[item.Key] = item.Value;
        }

        return output;
    }

    public static RegisterResponse FromPbRegisterResponse(Proto.PipeRegisterPluginResp? input)
    {
        if (input is null)
        {
            return new RegisterResponse();
        }

        return new RegisterResponse
        {
            Success = input.Success,
            Message = input.Message,
            InstanceId = input.ClhInstanceId,
            ServerInfo = FromPbServerInfo(input.ServerInfo),
            TimestampUtc = FromTimestamp(input.Timestamp)
        };
    }

    public static ServerInfo FromPbServerInfo(Proto.PipeServerInfo? input)
    {
        if (input is null)
        {
            return new ServerInfo();
        }

        return new ServerInfo
        {
            InstanceId = input.ClhInstanceId,
            Version = input.ClhVersion,
            KeepaliveTimeoutSec = input.KeepaliveTimeoutSec,
            ConnectedPluginCount = input.ConnectedPluginCount,
            UptimeSec = input.UptimeSec
        };
    }

    public static PluginTelemetry FromPbPluginTelemetry(Proto.PipePluginTelemetry? input)
    {
        if (input is null)
        {
            return new PluginTelemetry();
        }

        return new PluginTelemetry
        {
            PluginUuid = input.PluginUuid,
            ReceivedMessageCount = input.ReceivedMessageCount,
            SentMessageCount = input.SentMessageCount,
            ControlRequestCount = input.ControlRequestCount,
            ControlErrorCount = input.ControlErrorCount,
            LastRoundtripMs = input.LastRoundtripMs,
            UpdatedAtUtc = FromTimestamp(input.UpdatedAt)
        };
    }

    public static RigSnapshot FromPbRigSnapshot(Proto.PipeRigStatusSnapshot? input)
    {
        if (input is null)
        {
            return new RigSnapshot();
        }

        return new RigSnapshot
        {
            Provider = input.Provider,
            Endpoint = input.Endpoint,
            ServiceRunning = input.ServiceRunning,
            RigModel = input.RigModel,
            TxFrequencyHz = input.TxFrequencyHz,
            RxFrequencyHz = input.RxFrequencyHz,
            TxMode = input.TxMode,
            RxMode = input.RxMode,
            Split = input.Split,
            Power = input.Power,
            SampledAtUtc = FromTimestamp(input.SampledAt)
        };
    }

    public static UdpSnapshot FromPbUdpSnapshot(Proto.PipeUdpStatusSnapshot? input)
    {
        if (input is null)
        {
            return new UdpSnapshot();
        }

        return new UdpSnapshot
        {
            ServerRunning = input.ServerRunning,
            BindAddress = input.BindAddress,
            SampledAtUtc = FromTimestamp(input.SampledAt)
        };
    }

    public static QsoQueueSnapshot FromPbQsoQueueSnapshot(Proto.PipeQsoQueueSnapshot? input)
    {
        var output = new QsoQueueSnapshot
        {
            SampledAtUtc = FromTimestamp(input?.SampledAt)
        };

        if (input is null)
        {
            return output;
        }

        foreach (var detail in input.Details)
        {
            output.Details.Add(FromPbQsoDetail(detail));
        }

        return output;
    }

    public static SettingsSnapshot FromPbSettingsSnapshot(Proto.PipeMainSettingsSnapshot? input)
    {
        if (input is null)
        {
            return new SettingsSnapshot();
        }

        return new SettingsSnapshot
        {
            InstanceName = input.InstanceName,
            Language = input.Language,
            EnablePlugin = input.EnablePlugin,
            DisableAllCharts = input.DisableAllCharts,
            MyMaidenheadGrid = input.MyMaidenheadGrid,
            AutoQsoUploadEnabled = input.AutoQsoUploadEnabled,
            AutoRigUploadEnabled = input.AutoRigUploadEnabled,
            EnableUdpServer = input.EnableUdpServer,
            SampledAtUtc = FromTimestamp(input.SampledAt)
        };
    }

    public static RuntimeSnapshot FromPbRuntimeSnapshot(Proto.PipeRuntimeSnapshot? input)
    {
        if (input is null)
        {
            return new RuntimeSnapshot();
        }

        var output = new RuntimeSnapshot
        {
            ServerInfo = FromPbServerInfo(input.ServerInfo),
            RigSnapshot = FromPbRigSnapshot(input.RigSnapshot),
            UdpSnapshot = FromPbUdpSnapshot(input.UdpSnapshot),
            SettingsSnapshot = FromPbSettingsSnapshot(input.SettingsSnapshot),
            SampledAtUtc = FromTimestamp(input.SampledAt)
        };

        foreach (var telemetry in input.PluginTelemetry)
        {
            output.PluginTelemetry.Add(FromPbPluginTelemetry(telemetry));
        }

        return output;
    }

    public static PluginInfo FromPbPluginInfo(Proto.PipePluginInfo? input)
    {
        var output = new PluginInfo();
        if (input is null)
        {
            return output;
        }

        output.Uuid = input.Uuid;
        output.Name = input.Name;
        output.Version = input.Version;
        output.Description = input.Description;
        output.RegisteredAtUtc = FromTimestamp(input.RegisteredAt);
        output.LastHeartbeatUtc = FromTimestamp(input.LastHeartbeat);
        output.EventSubscription = FromPbEventSubscription(input.EventSubscription);
        output.Telemetry = FromPbPluginTelemetry(input.Telemetry);

        foreach (var item in input.Metadata)
        {
            output.Metadata[item.Key] = item.Value;
        }

        return output;
    }

    public static PluginList FromPbPluginList(Proto.PipePluginList? input)
    {
        var output = new PluginList();
        if (input is null)
        {
            return output;
        }

        foreach (var plugin in input.Plugins)
        {
            output.Plugins.Add(FromPbPluginInfo(plugin));
        }

        return output;
    }

    public static RigData FromPbRigData(Proto.RigData? input)
    {
        if (input is null)
        {
            return new RigData();
        }

        return new RigData
        {
            Uuid = input.Uuid,
            Provider = input.Provider,
            RigName = input.RigName,
            Frequency = input.Frequency,
            Mode = input.Mode,
            FrequencyRx = input.FrequencyRx,
            ModeRx = input.ModeRx,
            Split = input.Split,
            Power = input.Power,
            TimestampUtc = FromTimestamp(input.Timestamp)
        };
    }

    public static ClhInternalMessage FromPbInternal(Proto.ClhInternalMessage? input)
    {
        var output = new ClhInternalMessage
        {
            TimestampUtc = FromTimestamp(input?.Timestamp)
        };

        if (input is null)
        {
            return output;
        }

        switch (input.PayloadCase)
        {
            case Proto.ClhInternalMessage.PayloadOneofCase.QsoUploadStatus:
                output.QsoUploadStatus = FromPbQsoUploadStatus(input.QsoUploadStatus);
                break;
            case Proto.ClhInternalMessage.PayloadOneofCase.PluginLifecycle:
                output.PluginLifecycle = FromPbPluginLifecycle(input.PluginLifecycle);
                break;
            case Proto.ClhInternalMessage.PayloadOneofCase.ServerStatus:
                output.ServerStatus = FromPbServerStatus(input.ServerStatus);
                break;
            case Proto.ClhInternalMessage.PayloadOneofCase.QsoQueueStatus:
                output.QsoQueueStatus = FromPbQsoQueueStatus(input.QsoQueueStatus);
                break;
            case Proto.ClhInternalMessage.PayloadOneofCase.SettingsChanged:
                output.SettingsChanged = FromPbSettingsChanged(input.SettingsChanged);
                break;
            case Proto.ClhInternalMessage.PayloadOneofCase.PluginTelemetry:
                output.PluginTelemetry = FromPbPluginTelemetryChanged(input.PluginTelemetry);
                break;
        }

        return output;
    }

    public static QsoUploadStatusChanged? FromPbQsoUploadStatus(Proto.ClhQSOUploadStatusChanged? input)
    {
        if (input is null)
        {
            return null;
        }

        return new QsoUploadStatusChanged
        {
            Detail = input.Detail is null ? null : FromPbQsoDetail(input.Detail)
        };
    }

    public static QsoDetail FromPbQsoDetail(Proto.ClhQSODetail? input)
    {
        var output = new QsoDetail();
        if (input is null)
        {
            return output;
        }

        output.OriginalCountryName = input.OriginalCountryName;
        output.CqZone = input.CqZone;
        output.ItuZone = input.ItuZone;
        output.Continent = input.Continent;
        output.Latitude = input.Latitude;
        output.Longitude = input.Longitude;
        output.GmtOffset = input.GmtOffset;
        output.Dxcc = input.Dxcc;
        output.DateTimeOffUtc = FromTimestamp(input.DateTimeOff);
        output.DxCall = input.DxCall;
        output.DxGrid = input.DxGrid;
        output.TxFrequencyHz = input.TxFrequencyInHz;
        output.TxFrequencyMeters = input.TxFrequencyInMeters;
        output.Mode = input.Mode;
        output.ParentMode = input.ParentMode;
        output.ReportSent = input.ReportSent;
        output.ReportReceived = input.ReportReceived;
        output.TxPower = input.TxPower;
        output.Comments = input.Comments;
        output.Name = input.Name;
        output.DateTimeOnUtc = FromTimestamp(input.DateTimeOn);
        output.OperatorCall = input.OperatorCall;
        output.MyCall = input.MyCall;
        output.MyGrid = input.MyGrid;
        output.ExchangeSent = input.ExchangeSent;
        output.ExchangeReceived = input.ExchangeReceived;
        output.AdifPropagationMode = input.AdifPropagationMode;
        output.ClientId = input.ClientId;
        output.RawData = input.RawData;
        output.FailReason = input.FailReason;
        output.UploadStatus = (UploadStatus)input.UploadStatus;
        output.ForcedUpload = input.ForcedUpload;
        output.Uuid = input.Uuid;

        foreach (var item in input.UploadedServices)
        {
            output.UploadedServices[item.Key] = item.Value;
        }

        foreach (var item in input.UploadedServicesErrorMessage)
        {
            output.UploadedServicesErrorMessage[item.Key] = item.Value;
        }

        return output;
    }

    public static PluginLifecycleChanged? FromPbPluginLifecycle(Proto.ClhPluginLifecycleChanged? input)
    {
        if (input is null)
        {
            return null;
        }

        return new PluginLifecycleChanged
        {
            PluginUuid = input.PluginUuid,
            PluginName = input.PluginName,
            PluginVersion = input.PluginVersion,
            Reason = input.Reason,
            EventType = (PluginLifecycleEventType)input.EventType,
            EventTimeUtc = FromTimestamp(input.EventTime)
        };
    }

    public static ServerStatusChanged? FromPbServerStatus(Proto.ClhServerStatusChanged? input)
    {
        if (input is null)
        {
            return null;
        }

        return new ServerStatusChanged
        {
            InstanceId = input.ClhInstanceId,
            Version = input.ClhVersion,
            ConnectedPluginCount = input.ConnectedPluginCount,
            EventTimeUtc = FromTimestamp(input.EventTime)
        };
    }

    public static QsoQueueStatusChanged? FromPbQsoQueueStatus(Proto.ClhQsoQueueStatusChanged? input)
    {
        if (input is null)
        {
            return null;
        }

        return new QsoQueueStatusChanged
        {
            PendingCount = input.PendingCount,
            UploadedTotal = input.UploadedTotal,
            FailedTotal = input.FailedTotal,
            EventTimeUtc = FromTimestamp(input.EventTime)
        };
    }

    public static SettingsChanged? FromPbSettingsChanged(Proto.ClhSettingsChanged? input)
    {
        if (input is null)
        {
            return null;
        }

        return new SettingsChanged
        {
            ChangedPart = input.ChangedPart,
            Summary = input.Summary,
            EventTimeUtc = FromTimestamp(input.EventTime)
        };
    }

    public static PluginTelemetryChanged? FromPbPluginTelemetryChanged(Proto.ClhPluginTelemetryChanged? input)
    {
        if (input is null)
        {
            return null;
        }

        return new PluginTelemetryChanged
        {
            PluginUuid = input.PluginUuid,
            ReceivedMessageCount = input.ReceivedMessageCount,
            SentMessageCount = input.SentMessageCount,
            ControlRequestCount = input.ControlRequestCount,
            ControlErrorCount = input.ControlErrorCount,
            LastRoundtripMs = input.LastRoundtripMs,
            EventTimeUtc = FromTimestamp(input.EventTime)
        };
    }

    public static WsjtxMessage FromPbWsjtxMessage(Proto.WsjtxMessage? input)
    {
        var output = new WsjtxMessage();
        if (input is null)
        {
            return output;
        }

        output.Header = new WsjtxMessageHeader
        {
            MagicNumber = input.Header?.MagicNumber ?? 0,
            SchemaNumber = input.Header?.SchemaNumber ?? 0,
            Type = (WsjtxMessageType)(input.Header?.Type ?? Proto.MessageType.Heartbeat),
            Id = input.Header?.Id ?? string.Empty
        };
        output.TimestampUtc = FromTimestamp(input.Timestamp);

        if (input.Heartbeat is not null)
        {
            output.Heartbeat = new WsjtxHeartbeat
            {
                MaxSchemaNumber = input.Heartbeat.MaxSchemaNumber,
                Version = input.Heartbeat.Version,
                Revision = input.Heartbeat.HasRevision ? input.Heartbeat.Revision : null
            };
        }

        if (input.Status is not null)
        {
            var status = input.Status;
            output.Status = new WsjtxStatus
            {
                DialFrequency = status.DialFrequency,
                Mode = status.Mode,
                DxCall = status.DxCall,
                Report = status.Report,
                TxMode = status.TxMode,
                TxEnabled = status.TxEnabled,
                Transmitting = status.Transmitting,
                Decoding = status.Decoding,
                RxDf = status.RxDf,
                TxDf = status.TxDf,
                DeCall = status.DeCall,
                DeGrid = status.DeGrid,
                DxGrid = status.DxGrid,
                TxWatchdog = status.TxWatchdog,
                SubMode = status.SubMode,
                FastMode = status.FastMode,
                SpecialOpMode = status.HasSpecialOpMode ? (SpecialOperationMode)status.SpecialOpMode : null,
                FrequencyTolerance = status.HasFrequencyTolerance ? status.FrequencyTolerance : null,
                TrPeriod = status.HasTrPeriod ? status.TrPeriod : null,
                ConfigName = status.HasConfigName ? status.ConfigName : null,
                TxMessage = status.HasTxMessage ? status.TxMessage : null
            };
        }

        if (input.Decode is not null)
        {
            output.Decode = new WsjtxDecode
            {
                IsNew = input.Decode.IsNew,
                TimeUtc = FromTimestamp(input.Decode.Time),
                Snr = input.Decode.Snr,
                DeltaTime = input.Decode.DeltaTime,
                DeltaFrequency = input.Decode.DeltaFrequency,
                Mode = input.Decode.Mode,
                MessageText = input.Decode.Message,
                LowConfidence = input.Decode.LowConfidence,
                OffAir = input.Decode.OffAir
            };
        }

        if (input.Clear is not null)
        {
            output.Clear = new WsjtxClear
            {
                Window = (ClearWindow)input.Clear.Window
            };
        }

        if (input.Reply is not null)
        {
            output.Reply = new WsjtxReply
            {
                TimeUtc = FromTimestamp(input.Reply.Time),
                Snr = input.Reply.Snr,
                DeltaTime = input.Reply.DeltaTime,
                DeltaFrequency = input.Reply.DeltaFrequency,
                Mode = input.Reply.Mode,
                MessageText = input.Reply.Message,
                LowConfidence = input.Reply.LowConfidence,
                Modifiers = input.Reply.Modifiers
            };
        }

        if (input.QsoLogged is not null)
        {
            output.QsoLogged = new WsjtxQsoLogged
            {
                DateTimeOffUtc = FromTimestamp(input.QsoLogged.DatetimeOff),
                DxCall = input.QsoLogged.DxCall,
                DxGrid = input.QsoLogged.DxGrid,
                TxFrequency = input.QsoLogged.TxFrequency,
                Mode = input.QsoLogged.Mode,
                ReportSent = input.QsoLogged.ReportSent,
                ReportReceived = input.QsoLogged.ReportReceived,
                TxPower = input.QsoLogged.TxPower,
                Comments = input.QsoLogged.Comments,
                DateTimeOnUtc = FromTimestamp(input.QsoLogged.DatetimeOn),
                OperatorCall = input.QsoLogged.OperatorCall,
                MyCall = input.QsoLogged.MyCall,
                MyGrid = input.QsoLogged.MyGrid,
                ExchangeSent = input.QsoLogged.HasExchangeSent ? input.QsoLogged.ExchangeSent : null,
                ExchangeReceived = input.QsoLogged.HasExchangeReceived ? input.QsoLogged.ExchangeReceived : null,
                AdifPropagationMode = input.QsoLogged.HasAdifPropagationMode
                    ? input.QsoLogged.AdifPropagationMode
                    : null
            };
        }

        if (input.Close is not null)
        {
            output.Close = new WsjtxClose();
        }

        if (input.HaltTx is not null)
        {
            output.HaltTx = new WsjtxHaltTx
            {
                AutoTxOnly = input.HaltTx.AutoTxOnly
            };
        }

        if (input.FreeText is not null)
        {
            output.FreeText = new WsjtxFreeText
            {
                Text = input.FreeText.Text,
                Send = input.FreeText.Send
            };
        }

        if (input.WsprDecode is not null)
        {
            output.WsprDecode = new WsjtxWsprDecode
            {
                IsNew = input.WsprDecode.IsNew,
                TimeUtc = FromTimestamp(input.WsprDecode.Time),
                Snr = input.WsprDecode.Snr,
                DeltaTime = input.WsprDecode.DeltaTime,
                Frequency = input.WsprDecode.Frequency,
                Drift = input.WsprDecode.Drift,
                Callsign = input.WsprDecode.Callsign,
                Grid = input.WsprDecode.Grid,
                Power = input.WsprDecode.Power,
                OffAir = input.WsprDecode.HasOffAir ? input.WsprDecode.OffAir : null
            };
        }

        if (input.Location is not null)
        {
            output.Location = new WsjtxLocation
            {
                Location = input.Location.Location_
            };
        }

        if (input.LoggedAdif is not null)
        {
            output.LoggedAdif = new WsjtxLoggedAdif
            {
                AdifText = input.LoggedAdif.AdifText
            };
        }

        if (input.HighlightCallsign is not null)
        {
            output.HighlightCallsign = new WsjtxHighlightCallsign
            {
                Callsign = input.HighlightCallsign.Callsign,
                BackgroundColor = input.HighlightCallsign.BackgroundColor,
                ForegroundColor = input.HighlightCallsign.ForegroundColor,
                HighlightLast = input.HighlightCallsign.HighlightLast
            };
        }

        if (input.SwitchConfiguration is not null)
        {
            output.SwitchConfiguration = new WsjtxSwitchConfiguration
            {
                ConfigName = input.SwitchConfiguration.ConfigName
            };
        }

        if (input.Configure is not null)
        {
            output.Configure = new WsjtxConfigure
            {
                Mode = input.Configure.Mode,
                FrequencyTolerance = input.Configure.FrequencyTolerance,
                SubMode = input.Configure.SubMode,
                FastMode = input.Configure.FastMode,
                TrPeriod = input.Configure.TrPeriod,
                RxDf = input.Configure.RxDf,
                DxCall = input.Configure.DxCall,
                DxGrid = input.Configure.DxGrid,
                GenerateMessages = input.Configure.GenerateMessages
            };
        }

        return output;
    }

    public static PackedDecodeMessage FromPbPackedDecode(Proto.PackedDecodeMessage? input)
    {
        var output = new PackedDecodeMessage
        {
            TimestampUtc = FromTimestamp(input?.Timestamp)
        };

        if (input is null)
        {
            return output;
        }

        foreach (var item in input.Messages)
        {
            output.Messages.Add(new WsjtxDecode
            {
                IsNew = item.IsNew,
                TimeUtc = FromTimestamp(item.Time),
                Snr = item.Snr,
                DeltaTime = item.DeltaTime,
                DeltaFrequency = item.DeltaFrequency,
                Mode = item.Mode,
                MessageText = item.Message,
                LowConfidence = item.LowConfidence,
                OffAir = item.OffAir
            });
        }

        return output;
    }

    public static Envelope FromPbEnvelope(Proto.PipeEnvelope? input)
    {
        var output = new Envelope();
        if (input is null)
        {
            return output;
        }

        output.Id = input.Id;
        output.CorrelationId = input.CorrelationId;
        output.Kind = (EnvelopeKind)input.Kind;
        output.Topic = (EnvelopeTopic)input.Topic;
        output.Success = input.Success;
        output.MessageText = input.Message;
        output.ErrorCode = input.ErrorCode;
        output.TimestampUtc = FromTimestamp(input.Timestamp);

        foreach (var item in input.Attributes)
        {
            output.Attributes[item.Key] = item.Value;
        }

        if (input.Subscription is not null)
        {
            output.Subscription = FromPbEventSubscription(input.Subscription);
        }

        if (input.Payload is not null)
        {
            output.Payload = DecodeEnvelopePayload(input.Payload);
        }

        return output;
    }

    public static InboundConversionResult FromAnyMessage(Any anyMessage)
    {
        try
        {
            if (anyMessage.Is(Proto.RigData.Descriptor))
            {
                var pbMsg = anyMessage.Unpack<Proto.RigData>();
                var model = FromPbRigData(pbMsg);
                return new InboundConversionResult(
                    pbMsg,
                    new Message
                    {
                        Kind = InboundKind.RigData,
                        TimestampUtc = model.TimestampUtc,
                        RigData = model
                    });
            }

            if (anyMessage.Is(Proto.ClhInternalMessage.Descriptor))
            {
                var pbMsg = anyMessage.Unpack<Proto.ClhInternalMessage>();
                var model = FromPbInternal(pbMsg);
                return new InboundConversionResult(
                    pbMsg,
                    new Message
                    {
                        Kind = InboundKind.ClhInternal,
                        TimestampUtc = model.TimestampUtc,
                        ClhInternal = model
                    });
            }

            if (anyMessage.Is(Proto.PipeEnvelope.Descriptor))
            {
                var pbMsg = anyMessage.Unpack<Proto.PipeEnvelope>();
                var model = FromPbEnvelope(pbMsg);
                return new InboundConversionResult(
                    pbMsg,
                    new Message
                    {
                        Kind = InboundKind.Envelope,
                        TimestampUtc = model.TimestampUtc,
                        Envelope = model
                    });
            }

            if (anyMessage.Is(Proto.PipeConnectionClosed.Descriptor))
            {
                var pbMsg = anyMessage.Unpack<Proto.PipeConnectionClosed>();
                var model = new ConnectionClosed
                {
                    TimestampUtc = FromTimestamp(pbMsg.Timestamp)
                };
                return new InboundConversionResult(
                    pbMsg,
                    new Message
                    {
                        Kind = InboundKind.ConnectionClosed,
                        TimestampUtc = model.TimestampUtc,
                        ConnectionClosed = model
                    });
            }

            return UnknownInbound(anyMessage);
        }
        catch
        {
            return UnknownInbound(anyMessage);
        }
    }

    private static Message UnknownMessageFromAny(Any anyMessage)
    {
        return new Message
        {
            Kind = InboundKind.Unknown,
            Unknown = new UnknownMessage
            {
                TypeUrl = anyMessage.TypeUrl,
                Raw = anyMessage.Value.ToByteArray()
            }
        };
    }

    private static InboundConversionResult UnknownInbound(Any anyMessage)
    {
        return new InboundConversionResult(null, UnknownMessageFromAny(anyMessage));
    }

    private static object? DecodeEnvelopePayload(Any payload)
    {
        try
        {
            if (payload.Is(Proto.PipeServerInfo.Descriptor)) return FromPbServerInfo(payload.Unpack<Proto.PipeServerInfo>());
            if (payload.Is(Proto.PipePluginList.Descriptor)) return FromPbPluginList(payload.Unpack<Proto.PipePluginList>());
            if (payload.Is(Proto.PipeRuntimeSnapshot.Descriptor))
                return FromPbRuntimeSnapshot(payload.Unpack<Proto.PipeRuntimeSnapshot>());
            if (payload.Is(Proto.PipeRigStatusSnapshot.Descriptor))
                return FromPbRigSnapshot(payload.Unpack<Proto.PipeRigStatusSnapshot>());
            if (payload.Is(Proto.PipeUdpStatusSnapshot.Descriptor))
                return FromPbUdpSnapshot(payload.Unpack<Proto.PipeUdpStatusSnapshot>());
            if (payload.Is(Proto.PipeQsoQueueSnapshot.Descriptor))
                return FromPbQsoQueueSnapshot(payload.Unpack<Proto.PipeQsoQueueSnapshot>());
            if (payload.Is(Proto.PipeMainSettingsSnapshot.Descriptor))
                return FromPbSettingsSnapshot(payload.Unpack<Proto.PipeMainSettingsSnapshot>());
            if (payload.Is(Proto.PipePluginTelemetry.Descriptor))
                return FromPbPluginTelemetry(payload.Unpack<Proto.PipePluginTelemetry>());
            if (payload.Is(Proto.PipeEventSubscription.Descriptor))
                return FromPbEventSubscription(payload.Unpack<Proto.PipeEventSubscription>());
            if (payload.Is(Proto.ClhServerStatusChanged.Descriptor))
                return FromPbServerStatus(payload.Unpack<Proto.ClhServerStatusChanged>());
            if (payload.Is(Proto.ClhPluginLifecycleChanged.Descriptor))
                return FromPbPluginLifecycle(payload.Unpack<Proto.ClhPluginLifecycleChanged>());
            if (payload.Is(Proto.ClhQSOUploadStatusChanged.Descriptor))
                return FromPbQsoUploadStatus(payload.Unpack<Proto.ClhQSOUploadStatusChanged>());
            if (payload.Is(Proto.ClhQsoQueueStatusChanged.Descriptor))
                return FromPbQsoQueueStatus(payload.Unpack<Proto.ClhQsoQueueStatusChanged>());
            if (payload.Is(Proto.ClhSettingsChanged.Descriptor))
                return FromPbSettingsChanged(payload.Unpack<Proto.ClhSettingsChanged>());
            if (payload.Is(Proto.ClhPluginTelemetryChanged.Descriptor))
                return FromPbPluginTelemetryChanged(payload.Unpack<Proto.ClhPluginTelemetryChanged>());
            if (payload.Is(Proto.WsjtxMessage.Descriptor))
                return FromPbWsjtxMessage(payload.Unpack<Proto.WsjtxMessage>());
            if (payload.Is(Proto.PackedDecodeMessage.Descriptor))
                return FromPbPackedDecode(payload.Unpack<Proto.PackedDecodeMessage>());
            if (payload.Is(Proto.RigData.Descriptor)) return FromPbRigData(payload.Unpack<Proto.RigData>());
            if (payload.Is(Proto.ClhInternalMessage.Descriptor))
                return FromPbInternal(payload.Unpack<Proto.ClhInternalMessage>());

            return new UnknownMessage
            {
                TypeUrl = payload.TypeUrl,
                Raw = payload.Value.ToByteArray()
            };
        }
        catch
        {
            return new UnknownMessage
            {
                TypeUrl = payload.TypeUrl,
                Raw = payload.Value.ToByteArray()
            };
        }
    }

    private static DateTime FromTimestamp(Timestamp? input)
    {
        return input?.ToDateTime().ToUniversalTime() ?? DateTime.MinValue;
    }
}

