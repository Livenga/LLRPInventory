using System;
using System.Linq;
using System.Timers;

using Org.LLRP.LTK.LLRPV1;
using Org.LLRP.LTK.LLRPV1.DataType;
using Org.LLRP.LTK.LLRPV1.Impinj;


namespace LLRPInventory.UhfRfid {
  using ServerTimer = System.Timers.Timer;


  /// <summary></summary>
  public class ImpinjR420Reader : IUhfReader {
    /// <summary></summary>
    public bool IsConnected => this.baseClient?.IsConnected ?? false;

    /// <summary></summary>
    public bool IsReading => this.isReading;

    /// <summary></summary>
    public event ConnectionLostEventHandler? ConnectionLost = null;


    private LLRPClient? baseClient = null;
    private bool isReading = false;
    private int countAntennas = 4;


    private readonly string host;
    private readonly int port;
    private readonly int timeout;
    private readonly ServerTimer intervalTimer;


    /// <summary></summary>
    public ImpinjR420Reader(string host, int port, int timeout) {
      this.host = host;
      this.port = port;
      this.timeout = timeout;

      this.intervalTimer = new ServerTimer(5000f);
      this.intervalTimer.AutoReset = true;
      this.intervalTimer.Elapsed += this.OnIntervalTimerElapsed;
    }


    /// <summary></summary>
    public void Open() {
      if(this.IsConnected) {
        return;
      }

      this.baseClient = new LLRPClient(this.port);

      ENUM_ConnectionAttemptStatusType status = ENUM_ConnectionAttemptStatusType.Success;
      bool isSuccessed = this.baseClient.Open(
          llrp_reader_name: this.host,
          status: out status,
          timeout: this.timeout,
          useTLS: false);

      if(! isSuccessed || status != ENUM_ConnectionAttemptStatusType.Success) {
        throw new Exception($"{this.host}: 接続失敗.({status.ToString()})");
      }

      this.baseClient.OnRoAccessReportReceived += this.OnLLRPClientRoAccessReportReceived;
      this.baseClient.OnReaderEventNotification += this.OnLLRPClientReaderEventNotification;
      this.baseClient.OnKeepAlive += this.OnLLRPClientKeepalive;

      this.keepalivedAt = DateTime.Now;

      this.ResetToFactoryDefault();
      this.EnableImpinjExtends();

      this.GetReaderConfig();
      this.SetReaderConfig();

      this.intervalTimer.Start();
    }


    /// <summary></summary>
    public void Close() {
      if(! this.IsConnected) {
        return;
      }

      this.intervalTimer.Stop();

      try {
        this.ResetToFactoryDefault();
        this.EnableImpinjExtends();
      } catch(Exception) {
      }

      this.baseClient?.Close();

      this.baseClient = null;
    }


    /// <summary></summary>
    public void Start() {
      if(this.isReading) {
        return;
      }

      this.AddROSpec(14150);

      this.EnableROSpec(14150);

      this.StartROSpec(14150);

      this.isReading = true;
    }


    /// <summary></summary>
    public void Stop() {
      if(! this.isReading) {
        return;
      }

      this.isReading = false;

      this.StopROSpec(14150);

      this.DisableROSpec(14150);

      this.DeleteROSpec(14150);
    }


    ///<summary></summary>
    public void Dispose() {
      try {
        this.Close();

      } catch(Exception) {
      } finally {
        this.intervalTimer.Dispose();
        this.baseClient = null;
      }
    }


    /// <summary></summary>
    private void ResetToFactoryDefault() {
      MSG_SET_READER_CONFIG msg = new MSG_SET_READER_CONFIG();
      msg.ResetToFactoryDefault = true;

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_SET_READER_CONFIG_RESPONSE? msgResp = this.baseClient?.SET_READER_CONFIG(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    private void EnableImpinjExtends() {
      MSG_IMPINJ_ENABLE_EXTENSIONS msg = new MSG_IMPINJ_ENABLE_EXTENSIONS();

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_CUSTOM_MESSAGE? msgResp = this.baseClient?.CUSTOM_MESSAGE(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    private void SetReaderConfig() {
      MSG_SET_READER_CONFIG msg = new MSG_SET_READER_CONFIG();

      // Keepalive
      PARAM_KeepaliveSpec pKeepalive = new PARAM_KeepaliveSpec();
      msg.KeepaliveSpec = pKeepalive;
      pKeepalive.KeepaliveTriggerType = ENUM_KeepaliveTriggerType.Periodic;
      pKeepalive.PeriodicTriggerValue = 15000;


      // Event notification
      PARAM_ReaderEventNotificationSpec pNotifySpec = new PARAM_ReaderEventNotificationSpec();
      msg.ReaderEventNotificationSpec = pNotifySpec;
      pNotifySpec.EventNotificationState = new PARAM_EventNotificationState[1];

      // ROSpecEvent
      PARAM_EventNotificationState pROSpecEvent = new PARAM_EventNotificationState();
      pNotifySpec.EventNotificationState[0] = pROSpecEvent;
      pROSpecEvent.EventType = ENUM_NotificationEventType.ROSpec_Event;
      pROSpecEvent.NotificationState = true;


      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_SET_READER_CONFIG_RESPONSE? msgResp = this.baseClient?.SET_READER_CONFIG(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    private void GetReaderConfig() {
      MSG_GET_READER_CONFIG msg = new MSG_GET_READER_CONFIG();
      msg.RequestedData = ENUM_GetReaderConfigRequestedData.All;

      PARAM_ImpinjRequestedData pRequestData = new PARAM_ImpinjRequestedData();
      msg.Custom.Add(pRequestData);
      pRequestData.RequestedData = ENUM_ImpinjRequestedDataType.All_Configuration;

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_GET_READER_CONFIG_RESPONSE? msgResp = this.baseClient?.GET_READER_CONFIG(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);

      if(msgResp != null) {
        this.countAntennas = msgResp.AntennaConfiguration.Length;
      }
    }


    /// <summary></summary>
    private void EnableROSpec(uint roSpecId) {
      MSG_ENABLE_ROSPEC msg = new MSG_ENABLE_ROSPEC();
      msg.ROSpecID = roSpecId;

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_ENABLE_ROSPEC_RESPONSE? msgResp = this.baseClient?.ENABLE_ROSPEC(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    private void DisableROSpec(uint roSpecId) {
      MSG_DISABLE_ROSPEC msg = new MSG_DISABLE_ROSPEC();
      msg.ROSpecID = roSpecId;

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_DISABLE_ROSPEC_RESPONSE? msgResp = this.baseClient?.DISABLE_ROSPEC(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    private void StartROSpec(uint roSpecId) {
      MSG_START_ROSPEC msg = new MSG_START_ROSPEC();
      msg.ROSpecID = roSpecId;

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_START_ROSPEC_RESPONSE? msgResp = this.baseClient?.START_ROSPEC(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);

      if(msgResp != null) {
#if DEBUG
        Console.Error.WriteLine($"[Debug] Message ID: {msgResp.MSG_ID}");
#endif
      }
    }


    /// <summary></summary>
    private void StopROSpec(uint roSpecId = 0) {
      MSG_STOP_ROSPEC msg = new MSG_STOP_ROSPEC();
      msg.ROSpecID = roSpecId;

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_STOP_ROSPEC_RESPONSE? msgResp = this.baseClient?.STOP_ROSPEC(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    private void AddROSpec(uint roSpecId) {
      MSG_ADD_ROSPEC msg = new MSG_ADD_ROSPEC();

      PARAM_ROSpec pROSpec = new PARAM_ROSpec();
      msg.ROSpec = pROSpec;

      pROSpec.ROSpecID = roSpecId;
      pROSpec.CurrentState = ENUM_ROSpecState.Disabled;
      pROSpec.Priority = 0;


      // Start, Stop Triggers
      PARAM_ROBoundarySpec pBoundary = new PARAM_ROBoundarySpec();
      pROSpec.ROBoundarySpec = pBoundary;

      PARAM_ROSpecStartTrigger pStartTrigger = new PARAM_ROSpecStartTrigger();
      pBoundary.ROSpecStartTrigger = pStartTrigger;
      pStartTrigger.ROSpecStartTriggerType = ENUM_ROSpecStartTriggerType.Null;

      PARAM_ROSpecStopTrigger pStopTrigger = new PARAM_ROSpecStopTrigger();
      pBoundary.ROSpecStopTrigger = pStopTrigger;
      //pStopTrigger.ROSpecStopTriggerType = ENUM_ROSpecStopTriggerType.Null;
      //pStopTrigger.DurationTriggerValue = 0;
      pStopTrigger.ROSpecStopTriggerType = ENUM_ROSpecStopTriggerType.Duration;
      pStopTrigger.DurationTriggerValue = 500;


      // Report Spec
      PARAM_ROReportSpec pReport = new PARAM_ROReportSpec();
      pROSpec.ROReportSpec = pReport;
      pReport.N = 1;
      pReport.ROReportTrigger = ENUM_ROReportTriggerType.Upon_N_Tags_Or_End_Of_ROSpec;

      PARAM_ImpinjTagReportContentSelector pImpinjContentSelector = new PARAM_ImpinjTagReportContentSelector();
      pReport.Custom.Add(pImpinjContentSelector);
      //
      pImpinjContentSelector.ImpinjEnablePeakRSSI = new PARAM_ImpinjEnablePeakRSSI();
      pImpinjContentSelector.ImpinjEnablePeakRSSI.PeakRSSIMode = ENUM_ImpinjPeakRSSIMode.Enabled;
      //
      pImpinjContentSelector.ImpinjEnableRFPhaseAngle = new PARAM_ImpinjEnableRFPhaseAngle();
      pImpinjContentSelector.ImpinjEnableRFPhaseAngle.RFPhaseAngleMode = ENUM_ImpinjRFPhaseAngleMode.Enabled;

      // Tag content selector
      PARAM_TagReportContentSelector pReportContentSelector = new PARAM_TagReportContentSelector();
      pReport.TagReportContentSelector = pReportContentSelector;
      pReportContentSelector.AirProtocolEPCMemorySelector = new UNION_AirProtocolEPCMemorySelector();

      PARAM_C1G2EPCMemorySelector pEpcMemorySelector = new PARAM_C1G2EPCMemorySelector();
      pReportContentSelector.AirProtocolEPCMemorySelector.Add(pEpcMemorySelector);
      pEpcMemorySelector.EnableCRC = true;
      pEpcMemorySelector.EnablePCBits = true;

      pReportContentSelector.EnableAccessSpecID = false;
      pReportContentSelector.EnableAntennaID = true;
      pReportContentSelector.EnableAccessSpecID = false;
      pReportContentSelector.EnableChannelIndex = true;
      pReportContentSelector.EnableFirstSeenTimestamp = true;
      pReportContentSelector.EnableInventoryParameterSpecID = false;
      pReportContentSelector.EnableLastSeenTimestamp = true;
      pReportContentSelector.EnablePeakRSSI = true;
      pReportContentSelector.EnableROSpecID = true;
      pReportContentSelector.EnableTagSeenCount = true;

      // AISpec
      pROSpec.SpecParameter = new UNION_SpecParameter();
      PARAM_AISpec pAI = new PARAM_AISpec();
      pROSpec.SpecParameter.Add(pAI);

      pAI.AntennaIDs = new UInt16Array();
      foreach(ushort idx in Enumerable.Range(1, this.countAntennas)) {
        pAI.AntennaIDs.Add(idx);
      }

      pAI.AISpecStopTrigger = new PARAM_AISpecStopTrigger();
      pAI.AISpecStopTrigger.AISpecStopTriggerType = ENUM_AISpecStopTriggerType.Null;
      pAI.InventoryParameterSpec = new PARAM_InventoryParameterSpec[1];

      PARAM_InventoryParameterSpec pInventory = new PARAM_InventoryParameterSpec();
      pAI.InventoryParameterSpec[0] = pInventory;

      pInventory.ProtocolID = ENUM_AirProtocols.EPCGlobalClass1Gen2;
      pInventory.InventoryParameterSpecID = 14151;
      pInventory.AntennaConfiguration = new PARAM_AntennaConfiguration[this.countAntennas];
      foreach(int idx in Enumerable.Range(0, this.countAntennas)) {
        PARAM_AntennaConfiguration pAntennaConfig = new PARAM_AntennaConfiguration();
        pInventory.AntennaConfiguration[idx] = pAntennaConfig;

        pAntennaConfig.AntennaID = (ushort)(idx + 1);

        // Transmitter
        pAntennaConfig.RFTransmitter = new PARAM_RFTransmitter();
        pAntennaConfig.RFTransmitter.TransmitPower = 21; // 15.00 [dBm]
        pAntennaConfig.RFTransmitter.ChannelIndex = 1;
        pAntennaConfig.RFTransmitter.HopTableID = 0;

        // RfSensitivity
        pAntennaConfig.RFReceiver = new PARAM_RFReceiver();
        pAntennaConfig.RFReceiver.ReceiverSensitivity = 1; // -80.00 [dBm]
      }


      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_ADD_ROSPEC_RESPONSE? msgResp = this.baseClient?.ADD_ROSPEC(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      Console.Error.WriteLine($"{msgResp?.ToString()}");
      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    private void DeleteROSpec(uint roSpecId = 0) {
      MSG_DELETE_ROSPEC msg = new MSG_DELETE_ROSPEC();
      msg.ROSpecID = roSpecId;

      MSG_ERROR_MESSAGE? msgErr = null;
      MSG_DELETE_ROSPEC_RESPONSE? msgResp = this.baseClient?.DELETE_ROSPEC(
          msg: msg,
          msg_err: out msgErr,
          time_out: this.timeout);

      LLRPHelper.CheckError(msgResp, msgErr);
    }


    /// <summary></summary>
    public void OnLLRPClientReaderEventNotification(MSG_READER_EVENT_NOTIFICATION msg) {
      if(msg.ReaderEventNotificationData != null) {
        PARAM_ROSpecEvent? pROSpecEvent = msg.ReaderEventNotificationData.ROSpecEvent;

        // ROSpec event
        if(pROSpecEvent != null) {
          switch(pROSpecEvent.EventType) {
            case ENUM_ROSpecEventType.Start_Of_ROSpec:
              break;

            case ENUM_ROSpecEventType.End_Of_ROSpec:
              if(this.isReading) {
                this.StartROSpec(pROSpecEvent.ROSpecID);
              }
              break;
          }
        }
      }
    }


    /// <summary></summary>
    private void OnLLRPClientRoAccessReportReceived(MSG_RO_ACCESS_REPORT msg) {
      if(msg.TagReportData == null) {
        return;
      }

      foreach(var data in msg.TagReportData) {
        string epc = string.Empty;

        // PC_bits, CRC
        ushort? pc = null;
        ushort? crc = null;
        for(int i = 0; i < data.AirProtocolTagData.Length; ++i) {
          switch(data.AirProtocolTagData[i]) {
            case PARAM_C1G2_PC _pc:
              pc = _pc.PC_Bits;
              break;

            case PARAM_C1G2_CRC _crc:
              crc = _crc.CRC;
              break;
          }
        }

        // Custom
        double? angle = null;
        double? rssi = null;
        for(int i = 0; i < data.Custom.Length; ++i) {
          switch(data.Custom[i]) {
            case PARAM_ImpinjRFPhaseAngle _angle:
              angle = (double)_angle.PhaseAngle * (360f / 4096f);
              break;

            case PARAM_ImpinjPeakRSSI _rssi:
              rssi = (double)_rssi.RSSI / 100f;
              break;
          }
        }

        switch(data.EPCParameter[0]) {
          case PARAM_EPC_96 _epc:
            epc = _epc.EPC.ToHexString();
            break;

          case PARAM_EPCData _epc:
            epc = _epc.EPC.ToHexString();
            break;
        }

        Console.Error.WriteLine($"{data.AntennaID?.AntennaID},{epc},{data.TagSeenCount?.TagCount},{angle},{rssi}");
      }
    }


    //
    private DateTime keepalivedAt = DateTime.Now;


    /// <summary></summary>
    private void OnLLRPClientKeepalive(MSG_KEEPALIVE msg) {
      keepalivedAt = DateTime.Now;
    }


    /// <summary></summary>
    private void OnIntervalTimerElapsed(object source, ElapsedEventArgs args) {
      double sec = (DateTime.Now - this.keepalivedAt).TotalSeconds;
      Console.Error.WriteLine($"[Debug] OnIntervalTimer Elapsed {sec}");

      if(sec >= 30f) {
        this.intervalTimer.Stop();
#if DEBUG
        Console.Error.WriteLine($"[Debug] {this.host} Disconnected");
#endif
        this.ConnectionLost?.Invoke(this);
      }
    }
  }
}
