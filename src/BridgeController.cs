using System;
using System.Threading;
using System.Threading.Tasks;

namespace User.SimagicBridge {
    public sealed class BridgeController : IDisposable {
        private readonly ISimProClient client;
        private readonly BridgeSession session;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly object frameGate = new object();
        private LightFrame latest;
        private DateTime frameTime;
        private Task worker;
        private int command;
        private volatile bool enabled;
        public volatile string Status = "Checking SimPro connection.";
        public volatile string Error = "";
        public bool Enabled { get { return enabled; } set { enabled = value; } }
        public bool Connected { get { return session.Connected; } }
        public string Firmware { get { return session.WheelFirmware ?? ""; } }
        public string BaseName { get { return session.BaseName ?? ""; } }
        public long Writes { get { return session.Writes; } }
        public int Latency { get { return session.LastRpcMilliseconds; } }
        public bool RecoveryPending { get { return session.HasRecovery; } }
        public BridgeController(ISimProClient client, string journalPath) { this.client = client; session = new BridgeSession(client, journalPath); }
        public void Start() { if(worker != null) throw new InvalidOperationException("Already started."); worker = Task.Run((Func<Task>)RunAsync); }
        public void Publish(LightFrame frame) { lock(frameGate) { latest = frame; frameTime = DateTime.UtcNow; } }
        public void RequestTest() { enabled = false; Interlocked.Exchange(ref command, 1); }
        public void RequestRestore() { enabled = false; Interlocked.Exchange(ref command, 2); }
        private async Task RunAsync() {
            DateTime lastConnect = DateTime.MinValue, lastCheck = DateTime.MinValue;
            try {
                while(!stop.IsCancellationRequested) {
                    try {
                        if (!session.Connected || (DateTime.UtcNow - lastConnect).TotalSeconds >= 3) {
                            await session.ConnectAsync().ConfigureAwait(false); lastConnect = DateTime.UtcNow;
                        }
                        int requested = Interlocked.Exchange(ref command, 0);
                        if(requested == 1) {
                            Status = "Button 1 lights up cyan for 3 seconds.";
                            await session.RestoreAsync().ConfigureAwait(false);
                            try { await session.TestButtonAsync().ConfigureAwait(false); }
                            finally { session.RestoreAsync().GetAwaiter().GetResult(); }
                            Status = "Light test complete. Original lights restored.";
                        } else if(requested == 2) {
                            await session.RestoreAsync().ConfigureAwait(false); Error = "";
                            Status = "Original lights restored.";
                        } else if(!enabled) {
                            if(session.HasRecovery) await session.RestoreAsync().ConfigureAwait(false);
                            Status = "GT Neo connected. Light control is off.";
                        } else {
                            LightFrame frame; DateTime published;
                            lock(frameGate) { frame = latest; published = frameTime; }
                            if(frame == null || (DateTime.UtcNow - published).TotalSeconds > 2) {
                                if(session.HasRecovery) await session.RestoreAsync().ConfigureAwait(false);
                                Status = "Waiting for GT Neo via SimPro. Enable its LED profile.";
                            } else {
                                await session.BeginAsync().ConfigureAwait(false);
                                if((DateTime.UtcNow - lastCheck).TotalSeconds >= 1) {
                                    await session.CheckPresetAsync().ConfigureAwait(false); lastCheck = DateTime.UtcNow;
                                }
                                await session.ApplyAsync(frame, () => enabled && !stop.IsCancellationRequested && Volatile.Read(ref command) == 0).ConfigureAwait(false);
                                Status = "SimHub is controlling lights through SimPro."; Error = "";
                            }
                        }
                        await Task.Delay(500, stop.Token).ConfigureAwait(false);
                    } catch(OperationCanceledException) { break; }
                    catch(Exception ex) {
                        enabled = false; session.MarkDisconnected();
                        Error = ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException
                            ? "SimPro is not responding. Start SimPro and check your wheelbase." : ex.Message;
                        Status = "Light control stopped. " + Error;
                        try { session.RestoreAsync().GetAwaiter().GetResult(); }
                        catch(Exception restoreError) { Error += " Restore pending. " + restoreError.Message; }
                        try { Task.Delay(2000, stop.Token).GetAwaiter().GetResult(); } catch(OperationCanceledException) { break; }
                    }
                }
            } finally {
                enabled = false;
                try { session.RestoreAsync().GetAwaiter().GetResult(); }
                catch(Exception ex) { Error = "Restore pending. " + ex.Message; }
            }
        }
        public void Dispose() {
            enabled = false; stop.Cancel();
            if(worker != null && !worker.Wait(TimeSpan.FromSeconds(10))) {
                // Do not tear down the HTTP client while the restore is still running.
                worker.ContinueWith(t => { client.Dispose(); stop.Dispose(); });
            } else { client.Dispose(); stop.Dispose(); }
        }
    }
}
