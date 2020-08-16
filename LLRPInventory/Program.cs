using System;
using System.Threading;
using System.Threading.Tasks;

using LLRPInventory.UhfRfid;


namespace LLRPInventory {
  /// <summary></summary>
  public class Program {
    /// <summary></summary>
    private static AutoResetEvent? autoResetEvent = null;


    /// <summary></summary>
    public static void Main(string[] args) {
      if(args.Length == 0) {
        return;
      }
      string host = args[0];


      try {
        using(IUhfReader reader = new ImpinjR420Reader(
              host: host,
              port: 5084,
              timeout: 3000)) {
          reader.ConnectionLost += OnIUhfReaderConnectionLost;
          autoResetEvent = new AutoResetEvent(false);

          reader.Open();
          reader.Start();

          autoResetEvent?.WaitOne();

          reader.Stop();
        }
      } catch(Exception except) {
        Console.Error.WriteLine($"{except.GetType().Name} [{except.Message}] [{except.StackTrace}]");
      } finally {
        autoResetEvent?.Dispose();
        autoResetEvent = null;
      }
    }


    /// <summary></summary>
    private static void OnIUhfReaderConnectionLost(IUhfReader source) {
      autoResetEvent?.Set();
    }
  }
}
