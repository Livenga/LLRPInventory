using System;

using Org.LLRP.LTK.LLRPV1;
using Org.LLRP.LTK.LLRPV1.DataType;


namespace LLRPInventory.UhfRfid {
  /// <summary></summary>
  public static class LLRPHelper {
    public static void CheckError(Message? message, MSG_ERROR_MESSAGE? error) {
      if(message == null && error == null) {
        throw new Exception("timeout");
      }

      PARAM_LLRPStatus? status = (PARAM_LLRPStatus?)message?.GetType()
        .GetField(name: "LLRPStatus")?
        .GetValue(message);

      if(status == null) {
        status = error?.LLRPStatus;
      }

      if(status == null) {
        throw new InvalidOperationException();
      }

      if(status.StatusCode != ENUM_StatusCode.M_Success) {
        throw new Exception($"{status.StatusCode}: {status.ErrorDescription ?? string.Empty}");
      }
    }
  }
}
