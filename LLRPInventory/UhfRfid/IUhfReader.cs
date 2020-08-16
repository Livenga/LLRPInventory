using System;

namespace LLRPInventory.UhfRfid {
  /// <summary></summary>
  public interface IUhfReader : IDisposable {
    event ConnectionLostEventHandler? ConnectionLost;

    bool IsConnected { get; }

    void Open();
    void Close();

    void Start();
    void Stop();
  }
}
