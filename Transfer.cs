using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using ZModem.Constants;
using ZModem.CRC;

namespace ZModem
{
    [ExcludeFromCodeCoverage]
    public class Transfer
    {
        private static readonly object PadLock = new object();

        // --- General Constants ---
        private const int TaskTimeout = 4;
        private const int ChunkSize = 2048; // Used for upload

        // --- Download-specific Constants ---
        private const int HeaderWaitTimeoutMs = 10000;
        private const int ReadByteTimeoutMs = 5000;
        private const int MaxReceiveRetries = 5;
        private const int MaxCrcErrorsBeforeAbort = 8;
        private const int EofMismatchRetries = 1;

        private readonly SerialPort SerialPort;
        private readonly bool WithDebug;

        // Internal RX buffer to avoid losing bytes read from the serial port
        private readonly Queue<byte> _rxBuffer = new Queue<byte>();

        /// <summary>
        /// Create transfer instance.
        /// </summary>
        public Transfer(SerialPort serialPort, bool withDebug = false)
        {
            SerialPort = serialPort;
            WithDebug = withDebug;
        }

        #region Upload Functionality (Unchanged)
        /// <summary>
        /// Upload a file
        /// </summary>
        public bool Upload(string filename)
        {
            var fileInfo = new FileInfo(filename);
            var data = File.ReadAllBytes(fileInfo.FullName);
            return Upload(fileInfo.Name, data, fileInfo.LastWriteTimeUtc);
        }

        /// <summary>
        /// Upload from memory
        /// </summary>
        public bool Upload(byte[] data)
        {
            var tempFile = Path.GetTempFileName();
            var fileInfo = new FileInfo(tempFile);
            var result = Upload(fileInfo.Name, data, DateTimeOffset.UtcNow);
            if (File.Exists(fileInfo.FullName))
            {
                File.Delete(fileInfo.FullName);
            }
            return result;
        }

        private bool Upload(string filename, byte[] data, DateTimeOffset lastWriteTimeUtc)
        {
            var hasUploadedSuccessfully = default(bool);
            var sw = new Stopwatch();
            sw.Start();
            var crc16 = new CRC16();
            var crc32 = new CRC32();
            CloseSerialPortIfOpen();
            var hasExecutedCommandSuccessfully = SendZRQINITFrame(crc16);
            if (hasExecutedCommandSuccessfully)
            {
                hasUploadedSuccessfully = hasExecutedCommandSuccessfully = SendZFILEHeaderCommand(filename, data.Length, lastWriteTimeUtc, crc32);
            }
            if (hasExecutedCommandSuccessfully)
            {
                SendZDATAPackets(data, 0, ChunkSize, crc32);
            }
            if (hasExecutedCommandSuccessfully)
            {
                hasUploadedSuccessfully = hasExecutedCommandSuccessfully = SendEOFCommand(data.Length, crc16);
            }
            hasExecutedCommandSuccessfully = SendFinishSession(crc16);
            SendCommand("OO");
            sw.Stop();
            Console.WriteLine($"Took: {sw.ElapsedMilliseconds}ms");
            return hasUploadedSuccessfully;
        }

        private bool SendZRQINITFrame(CRC16 crcCalculator)
        {
            var result = default(bool);
            var zrqinitFrame = Utils.BuildCommonHexHeader(HeaderType.ZRQINIT, 0, 0, 0, 0, crcCalculator);
            var response = SendCommand(zrqinitFrame, true, HeaderType.ZRINIT);
            if (response?.ZHeader == HeaderType.ZRINIT)
            {
                result = true;
            }
            return result;
        }

        private bool SendZFILEHeaderCommand(string filename, int length, DateTimeOffset lastWriteTimeUtc, CRC32 crcCalculator)
        {
            var result = default(bool);
            var isExtended = true;
            var zFileHeader = Utils.Build32BitBinHeader(HeaderType.ZFILE, ZFILEConversionOption.ZCBIN, ZFILEManagementOption.ZMNEWL, ZFILETransportOption.None, ZFILEExtendedOptions.None, crcCalculator);
            var zFileHeaderQueue = new Queue<byte>();
            foreach (var c in zFileHeader) { zFileHeaderQueue.Enqueue((byte)c); }
            SendCommand(zFileHeaderQueue.ToArray());
            var dataQueue = new Queue<byte>();
            foreach (char c in filename) { dataQueue.Enqueue((byte)c); }
            if (isExtended)
            {
                dataQueue.Enqueue(0);
                var fileLength = length.ToString();
                foreach (var c in fileLength) { dataQueue.Enqueue((byte)c); }
                dataQueue.Enqueue(0x20);
                var utcTime = lastWriteTimeUtc.ToUnixTimeSeconds();
                var octalString = Convert.ToString(utcTime, 8);
                foreach (var c in octalString) { dataQueue.Enqueue((byte)c); }
            }
            else
            {
                dataQueue.Enqueue(0);
                dataQueue.Enqueue(0);
            }
            byte[] data = dataQueue.Concat(new byte[] { (byte)ZDLESequence.ZCRCW }).ToArray();
            dataQueue.Enqueue((byte)ControlBytes.ZDLE);
            dataQueue.Enqueue((byte)ZDLESequence.ZCRCW);
            var crc = crcCalculator.ComputeHash(data);
            var encodedCRC = dataQueue.Concat(ZDLEEncoder.EscapeControlCharacters(crc)).ToArray();
            var response = SendCommand(encodedCRC, true, HeaderType.ZRPOS);
            if (response?.ZHeader == HeaderType.ZRPOS)
            {
                result = true;
            }
            return result;
        }

        private void SendZDATAPackets(byte[] src, int? offset, int chunkSize, CRC32 crcCalculator)
        {
            var zdataHeaderFrame = GenerateZDATAHeaderFrame(offset, crcCalculator);
            SendCommand(zdataHeaderFrame);
            ResponseHeader zdataResponse = null;
            var initOffset = offset.HasValue ? offset.Value : 0;
            for (int i = initOffset; i < src.Length; i += chunkSize)
            {
                if (zdataResponse?.ZHeader == HeaderType.ZRPOS)
                {
                    Console.WriteLine($"ZModem stumbled - (╯°□°)╯͡  ┻━┻");
                    Console.WriteLine("We have to revive it - Ȫ_Ȫ");
                }
                var dataSubpacketFrame = GenerateDataSubpacketFrame(crcCalculator, src, i, chunkSize);
                zdataResponse = SendCommand(dataSubpacketFrame, true);
                var progression = (i / (double)src.Length);
                Utils.WriteProgression(progression);
            }
        }

        private static byte[] GenerateDataSubpacketFrame(CRC32 crcCalculator, byte[] src, int offset, int chunkSize, ZDLESequence? forceZDLESequence = null)
        {
            var dataSlice = src.Skip(offset).Take(chunkSize).ToArray();
            var encodedDataSlice = ZDLEEncoder.EscapeControlCharacters(dataSlice);
            var requiredSequence = forceZDLESequence.HasValue ? forceZDLESequence.Value : (offset + dataSlice.Length) < src.Length ? ZDLESequence.ZCRCG : ZDLESequence.ZCRCE;
            var queue = new Queue<byte>(encodedDataSlice);
            var beforeCTC = dataSlice.Concat(new byte[] { (byte)requiredSequence }).ToArray();
            var crc = crcCalculator.ComputeHash(beforeCTC);
            queue.Enqueue((byte)ControlBytes.ZDLE);
            queue.Enqueue((byte)requiredSequence);
            var dataSubpacketFrame = queue?.ToArray();
            var encodedCRC = dataSubpacketFrame.Concat(ZDLEEncoder.EscapeControlCharacters(crc)).ToArray();
            return encodedCRC;
        }

        private byte[] GenerateZDATAHeaderFrame(int? offset, CRC32 crcCalculator)
        {
            Utils.GenerateZModemFileOffset(offset, out int p0, out int p1, out int p2, out int p3);
            var zdataHeaderQueue = new Queue<byte>();
            var zdataHeaderCommand = Utils.Build32BitDataHeader(HeaderType.ZDATA, p0, p1, p2, p3, crcCalculator);
            foreach (var c in zdataHeaderCommand) { zdataHeaderQueue.Enqueue((byte)c); }
            return zdataHeaderQueue.ToArray();
        }

        private bool SendEOFCommand(int dataLength, CRC16 crcCalculator)
        {
            var result = default(bool);
            Utils.GenerateZModemFileOffset(dataLength, out int p0, out int p1, out int p2, out int p3);
            var zeofCommand = Utils.BuildCommonHexHeader(HeaderType.ZEOF, p0, p1, p2, p3, crcCalculator);
            var response = SendCommand(zeofCommand, true, HeaderType.ZRINIT);
            if (response?.ZHeader == HeaderType.ZRINIT)
            {
                result = true;
            }
            return result;
        }

        private bool SendFinishSession(CRC16 crcCalculator)
        {
            var result = default(bool);
            var zfinCommand = Utils.BuildCommonHexHeader(HeaderType.ZFIN, 0, 0, 0, 0, crcCalculator);
            var response = SendCommand(zfinCommand, true, HeaderType.ZFIN);
            if (response?.ZHeader == HeaderType.ZFIN)
            {
                result = true;
            }
            return result;
        }

        private void PrepareSerialPort()
        {
            if (!SerialPort.IsOpen) SerialPort.Open();
            SerialPort.DiscardInBuffer();
            SerialPort.DiscardOutBuffer();

            // Set a short read timeout on the port to allow cancellation checks to be more responsive.
            try
            {
                SerialPort.ReadTimeout = 200; // ms
            }
            catch
            {
                // some SerialPort implementations may throw on setting ReadTimeout; ignore.
            }
        }

        private void CloseSerialPortIfOpen()
        {
            if (SerialPort.IsOpen) SerialPort.Close();
        }

        ResponseHeader SendCommand(string command, bool withResponse = false, HeaderType expectedResponse = HeaderType.None)
        {
            var rawCommand = Encoding.ASCII.GetBytes(command);
            return SendCommand(rawCommand, withResponse, expectedResponse);
        }

        ResponseHeader SendCommand(byte[] Command, bool withResponse = false, HeaderType expectedResponse = HeaderType.None)
        {
            ResponseHeader response = default(ResponseHeader);
            for (int i = 0; i < 3; i++) // ReadRetryAttempts from original
            {
                lock (PadLock)
                {
                    PrepareSerialPort();
                    SerialPort.Write(Command, 0, Command.Length);
                }
                Thread.Sleep(TaskTimeout);
                if (!withResponse) break;
                if (SerialPort.BytesToRead > 0)
                {
                    var buffer = new byte[SerialPort.BytesToRead];
                    lock (PadLock) { SerialPort.Read(buffer, 0, SerialPort.BytesToRead); }
                    response = new ResponseHeader(buffer);
                    if (response?.ZHeader == expectedResponse) break;
                    else Thread.Sleep(100); // ReadRetryAttemptTimeout
                }
                else if (expectedResponse == HeaderType.None) break;
                else Thread.Sleep(100); // ReadRetryAttemptTimeout
            }
            return response;
        }
        #endregion

        #region Combined Download Implementation

        /// <summary>
        /// Downloads files from a remote system using the ZMODEM protocol.
        /// This method handles multiple files, echo cancellation, file resume, and error recovery.
        /// </summary>
        /// <param name="destinationPath">The directory where downloaded files will be saved.</param>
        /// <param name="remoteSendCommand">Optional command to send to the remote to initiate the file transfer (e.g. "sz /etc/hostname\r").</param>
        /// <param name="ct">A CancellationToken to allow for cancelling the operation.</param>
        /// <returns>True if the entire session completed successfully, otherwise false.</returns>
        public bool Download(string destinationPath, string remoteSendCommand = null, CancellationToken ct = default)
        {
            var sw = new Stopwatch();
            sw.Start();

            // Ensure destination directory exists
            Directory.CreateDirectory(destinationPath);

            var crc16 = new CRC16();
            var crc32 = new CRC32();
            var echoFilter = new EchoFilter();

            ZModemReceiverState currentState = ZModemReceiverState.Idle;
            FileStream currentFileStream = null;
            FileReceiveInfo currentFileInfo = null;
            int retries = 0;
            bool sessionFinished = false;
            int eofMismatchAttempts = 0;

            try
            {
                CloseSerialPortIfOpen();
                PrepareSerialPort();

                // If a command is provided (e.g., "sz file.txt\r"), send it to start the transfer.
                if (!string.IsNullOrEmpty(remoteSendCommand))
                {
                    if (WithDebug) Console.WriteLine($"[DEBUG] Sending remote command: {remoteSendCommand}");
                    var commandBytes = Encoding.ASCII.GetBytes(remoteSendCommand);
                    SendCommandWithEcho(echoFilter, commandBytes);
                }

                // A standard ZMODEM receiver starts by sending ZRINIT to signal readiness.
                SendZRINIT(crc16, echoFilter);
                currentState = ZModemReceiverState.AwaitingFile;

                while (!sessionFinished && !ct.IsCancellationRequested)
                {
                    var header = ReadHeaderWithEchoFilter(echoFilter, HeaderWaitTimeoutMs, ct);

                    if (header == null)
                    {
                        if (retries++ < MaxReceiveRetries)
                        {
                            if (WithDebug) Console.WriteLine($"[DEBUG] Timeout waiting for header. Retrying ({retries}/{MaxReceiveRetries})...");
                            // Resend last packet based on state to prompt sender
                            switch (currentState)
                            {
                                case ZModemReceiverState.AwaitingFile:
                                    SendZRINIT(crc16, echoFilter);
                                    break;
                                case ZModemReceiverState.ReceivingData:
                                    if (currentFileStream != null)
                                        SendZRPOS(currentFileStream.Position, crc16, echoFilter);
                                    break;
                            }
                            continue;
                        }
                        if (WithDebug) Console.WriteLine("[ERROR] Maximum retries exceeded. Aborting session.");
                        currentState = ZModemReceiverState.Error;
                        break;
                    }

                    // A valid header was received, reset retry counter.
                    retries = 0;
                    if (WithDebug) Console.WriteLine($"[DEBUG] Received Header: {header.ZHeader}");

                    switch (header.ZHeader)
                    {
                        case HeaderType.ZFILE:
                            currentState = HandleZFileFrame(header, destinationPath, crc32, echoFilter, ct, out currentFileInfo, out currentFileStream);
                            break;

                        case HeaderType.ZDATA:
                            if (currentState == ZModemReceiverState.ReceivingData)
                            {
                                currentState = HandleZDataFrame(header, currentFileStream, crc16, crc32, echoFilter, ct);
                            }
                            else
                            {
                                if (WithDebug) Console.WriteLine($"[WARN] Received ZDATA in unexpected state: {currentState}. Ignoring.");
                            }
                            break;

                        case HeaderType.ZEOF:
                            if (currentState == ZModemReceiverState.ReceivingData)
                            {
                                var expectedOffset = Utils.GetIntFromZModemOffset(header.ZP0, header.ZP1, header.ZP2, header.ZP3);
                                if (WithDebug) Console.WriteLine($"[DEBUG] ZEOF received. Expected size: {expectedOffset}, Actual size: {currentFileStream?.Length ?? 0}");

                                if (currentFileStream != null && currentFileStream.Length == expectedOffset)
                                {
                                    if (WithDebug) Console.WriteLine($"[DEBUG] File '{currentFileInfo.Name}' received successfully.");
                                }
                                else
                                {
                                    if (WithDebug) Console.WriteLine($"[WARN] File size mismatch for '{currentFileInfo?.Name}'. Expected {expectedOffset}, got {currentFileStream?.Length ?? 0}.");
                                    if (eofMismatchAttempts < EofMismatchRetries && currentFileStream != null)
                                    {
                                        eofMismatchAttempts++;
                                        if (WithDebug) Console.WriteLine($"[DEBUG] Requesting resend from offset {currentFileStream.Length} (retry {eofMismatchAttempts}).");
                                        SendZRPOS(currentFileStream.Length, crc16, echoFilter);
                                        currentState = ZModemReceiverState.ReceivingData;
                                        break;
                                    }
                                }

                                currentFileStream?.Close();
                                currentFileStream = null;

                                // Signal readiness for the next file or ZFIN
                                SendZRINIT(crc16, echoFilter);
                                currentState = ZModemReceiverState.AwaitingFile;
                            }
                            break;

                        case HeaderType.ZFIN:
                            if (WithDebug) Console.WriteLine("[DEBUG] ZFIN received. Acknowledging and finishing session.");
                            SendZFIN(crc16, echoFilter);
                            SendCommandWithEcho(echoFilter, Encoding.ASCII.GetBytes("OO"));
                            currentState = ZModemReceiverState.Finished;
                            sessionFinished = true;
                            break;

                        case HeaderType.ZSINIT:
                            if (WithDebug) Console.WriteLine("[DEBUG] ZSINIT received, consuming subpacket and acknowledging.");
                            // consume subpacket (ZSINIT carries a data subpacket) then ack
                            var sinitData = ReceiveDataSubpacket(new CRC32(), echoFilter, ct, out bool sinitCrcValid);
                            SendZACK(0, crc16, echoFilter); // Acknowledge sender's options
                            break;

                        case HeaderType.ZABORT:
                            if (WithDebug) Console.WriteLine("[ERROR] ZABORT received from sender.");
                            currentState = ZModemReceiverState.Error;
                            sessionFinished = true;
                            break;

                        // Harmless echo or repeated requests
                        case HeaderType.ZRQINIT:
                        case HeaderType.ZRINIT:
                            if (WithDebug) Console.WriteLine($"[DEBUG] Received {header.ZHeader} (likely echo or retry), re-sending ZRINIT.");
                            SendZRINIT(crc16, echoFilter);
                            break;

                        default:
                            if (WithDebug) Console.WriteLine($"[WARN] Unhandled header received: {header.ZHeader}. Ignoring.");
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (WithDebug) Console.WriteLine("[DEBUG] Download operation was canceled.");
                currentState = ZModemReceiverState.Error;
            }
            catch (Exception ex)
            {
                if (WithDebug) Console.WriteLine($"[FATAL] An unhandled exception occurred during download: {ex}");
                currentState = ZModemReceiverState.Error;
            }
            finally
            {
                currentFileStream?.Close();

                // If the session didn't finish cleanly, send an abort sequence.
                if (currentState == ZModemReceiverState.Error)
                {
                    if (WithDebug) Console.WriteLine("[DEBUG] Session ended with an error. Sending abort sequence.");
                    SendAbortSequence();
                }

                CloseSerialPortIfOpen();
                sw.Stop();
                Console.WriteLine($"Download session finished in {sw.ElapsedMilliseconds}ms. Success: {currentState == ZModemReceiverState.Finished}");
            }

            return currentState == ZModemReceiverState.Finished;
        }

        /// <summary>
        /// Handles the logic for an incoming ZFILE frame, including file resume.
        /// </summary>
        private ZModemReceiverState HandleZFileFrame(ResponseHeader header, string destinationPath, CRC32 crc32, EchoFilter echoFilter, CancellationToken ct, out FileReceiveInfo fileInfo, out FileStream fileStream)
        {
            fileInfo = null;
            fileStream = null;

            if (WithDebug) Console.WriteLine("[DEBUG] Parsing ZFILE data subpacket...");
            var fileData = ReceiveDataSubpacket(crc32, echoFilter, ct, out var crcValid);
            if (fileData == null || !crcValid)
            {
                if (WithDebug) Console.WriteLine("[ERROR] Failed to receive or validate ZFILE data subpacket.");
                SendZNAK(new CRC16(), echoFilter);
                return ZModemReceiverState.Error;
            }

            fileInfo = ParseFileInformation(fileData);
            if (fileInfo == null)
            {
                if (WithDebug) Console.WriteLine("[ERROR] Could not parse file information.");
                SendZNAK(new CRC16(), echoFilter);
                return ZModemReceiverState.Error;
            }

            if (WithDebug) Console.WriteLine($"[DEBUG] Incoming file: '{fileInfo.Name}', Size: {fileInfo.Length}");

            var fullPath = Path.Combine(destinationPath, fileInfo.Name);
            long resumeOffset = 0;

            // --- File Resume Logic ---
            if (File.Exists(fullPath))
            {
                var existingLength = new FileInfo(fullPath).Length;
                if (existingLength >= fileInfo.Length)
                {
                    if (WithDebug) Console.WriteLine($"[DEBUG] File '{fileInfo.Name}' already exists and is complete. Skipping.");
                    SendZSKIP(new CRC16(), echoFilter);
                    return ZModemReceiverState.AwaitingFile; // Ready for next file
                }
                else
                {
                    resumeOffset = existingLength;
                    if (WithDebug) Console.WriteLine($"[DEBUG] Resuming transfer for '{fileInfo.Name}' from offset {resumeOffset}.");
                    fileStream = new FileStream(fullPath, FileMode.Append, FileAccess.Write);
                }
            }
            else
            {
                fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            }

            SendZRPOS(resumeOffset, new CRC16(), echoFilter);
            return ZModemReceiverState.ReceivingData;
        }

        /// <summary>
        /// Handles an incoming ZDATA header and the subsequent data stream.
        /// </summary>
        private ZModemReceiverState HandleZDataFrame(ResponseHeader header, FileStream fs, CRC16 crc16, CRC32 crc32, EchoFilter echoFilter, CancellationToken ct)
        {
            var dataOffset = Utils.GetIntFromZModemOffset(header.ZP0, header.ZP1, header.ZP2, header.ZP3);
            if (dataOffset != fs.Position)
            {
                if (WithDebug) Console.WriteLine($"[WARN] Data offset mismatch. Expected {fs.Position}, got {dataOffset}. Correcting.");
                SendZRPOS(fs.Position, crc16, echoFilter);
                return ZModemReceiverState.ReceivingData; // Remain in this state
            }

            bool success = ReceiveAndWriteDataSubpackets(fs, crc16, crc32, echoFilter, ct, expectedTotalLength: -1);
            return success ? ZModemReceiverState.ReceivingData : ZModemReceiverState.Error;
        }

        /// <summary>
        /// Receives and writes a stream of data subpackets to the file, handling CRC errors and retries.
        /// </summary>
        private bool ReceiveAndWriteDataSubpackets(FileStream fs, CRC16 crc16, CRC32 crc32, EchoFilter echoFilter, CancellationToken ct, long expectedTotalLength = -1)
        {
            int crcErrorCount = 0;
            while (!ct.IsCancellationRequested)
            {
                var data = ReceiveDataSubpacket(crc32, echoFilter, ct, out bool crcValid);

                if (data == null)
                {
                    if (WithDebug) Console.WriteLine("[ERROR] Failed to receive data subpacket.");
                    return false; // Fatal error
                }

                if (!crcValid)
                {
                    crcErrorCount++;
                    if (WithDebug) Console.WriteLine($"[WARN] CRC mismatch on data subpacket (error {crcErrorCount}/{MaxCrcErrorsBeforeAbort}).");

                    if (crcErrorCount >= MaxCrcErrorsBeforeAbort)
                    {
                        if (WithDebug) Console.WriteLine("[ERROR] Too many CRC errors. Aborting file transfer.");
                        return false;
                    }

                    // Request retransmission from the last known good position
                    SendZRPOS(fs.Position, crc16, echoFilter);
                    continue; // Try receiving the packet again
                }

                // CRC is valid, reset error count and write data
                crcErrorCount = 0;
                if (data.Data.Length > 0)
                {
                    fs.Write(data.Data, 0, data.Data.Length);
                }

                // Progress update - if expectedTotalLength provided use that; otherwise skip detailed progression
                if (WithDebug && expectedTotalLength > 0)
                    Utils.WriteProgression(fs.Position / (double)expectedTotalLength);
                else if (WithDebug && expectedTotalLength <= 0)
                    Utils.WriteProgression(0); // keep behavior consistent if unknown

                // ZCRCE marks the end of the ZDATA frame. The outer loop will now expect a ZEOF header.
                if (data.FrameEnd == ZDLESequence.ZCRCE)
                {
                    if (WithDebug) Console.WriteLine("[DEBUG] End of ZDATA frame received (ZCRCE).");
                    return true;
                }

                // ZCRCW/ZCRCQ are variants that may require an ACK, but for streaming, we can often just continue.
                // Modern implementations often don't wait for ACK and rely on ZRPOS for error correction.
                if (data.FrameEnd == ZDLESequence.ZCRCW || data.FrameEnd == ZDLESequence.ZCRCQ)
                {
                    // Optionally send a ZACK here if the sender seems to be waiting.
                    // SendZACK(fs.Position, crc16, echoFilter);
                }

                // ZCRCG means more data follows nonstop. Continue the loop.
            }
            return false; // Canceled
        }

        #endregion

        #region Low-Level ZMODEM Helpers

        // --- Sending Frames ---
        private void SendCommandWithEcho(EchoFilter ef, byte[] data)
        {
            ef?.NoteSent(data);
            lock (PadLock)
            {
                SerialPort.Write(data, 0, data.Length);
            }
            if (WithDebug) Console.WriteLine($"SENT> {Encoding.ASCII.GetString(data).Replace("\r", "\\r").Replace("\n", "\\n")}");
        }

        private void SendZRINIT(CRC16 crc, EchoFilter ef)
        {
            var frame = Utils.BuildCommonHexHeader(HeaderType.ZRINIT, 0, 0, 0, 0, crc);
            SendCommandWithEcho(ef, Encoding.ASCII.GetBytes(frame));
            if (WithDebug) Console.WriteLine("[DEBUG] Sent ZRINIT");
        }
        private void SendZRPOS(long offset, CRC16 crc, EchoFilter ef)
        {
            Utils.GenerateZModemFileOffset((int)offset, out int p0, out int p1, out int p2, out int p3);
            var frame = Utils.BuildCommonHexHeader(HeaderType.ZRPOS, p0, p1, p2, p3, crc);
            SendCommandWithEcho(ef, Encoding.ASCII.GetBytes(frame));
            if (WithDebug) Console.WriteLine($"[DEBUG] Sent ZRPOS for offset {offset}");
        }
        private void SendZACK(long offset, CRC16 crc, EchoFilter ef)
        {
            Utils.GenerateZModemFileOffset((int)offset, out int p0, out int p1, out int p2, out int p3);
            var frame = Utils.BuildCommonHexHeader(HeaderType.ZACK, p0, p1, p2, p3, crc);
            SendCommandWithEcho(ef, Encoding.ASCII.GetBytes(frame));
            if (WithDebug) Console.WriteLine($"[DEBUG] Sent ZACK for offset {offset}");
        }
        private void SendZSKIP(CRC16 crc, EchoFilter ef)
        {
            var frame = Utils.BuildCommonHexHeader(HeaderType.ZSKIP, 0, 0, 0, 0, crc);
            SendCommandWithEcho(ef, Encoding.ASCII.GetBytes(frame));
            if (WithDebug) Console.WriteLine("[DEBUG] Sent ZSKIP");
        }
        private void SendZNAK(CRC16 crc, EchoFilter ef)
        {
            var frame = Utils.BuildCommonHexHeader(HeaderType.ZNAK, 0, 0, 0, 0, crc);
            SendCommandWithEcho(ef, Encoding.ASCII.GetBytes(frame));
            if (WithDebug) Console.WriteLine("[DEBUG] Sent ZNAK");
        }
        private void SendZFIN(CRC16 crc, EchoFilter ef)
        {
            var frame = Utils.BuildCommonHexHeader(HeaderType.ZFIN, 0, 0, 0, 0, crc);
            SendCommandWithEcho(ef, Encoding.ASCII.GetBytes(frame));
            if (WithDebug) Console.WriteLine("[DEBUG] Sent ZFIN");
        }
        private void SendAbortSequence()
        {
            var abortSequence = new byte[] { 24, 24, 24, 24, 24, 24, 24, 24, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8 }; // CAN*8, BS*10
            lock (PadLock)
            {
                if (SerialPort.IsOpen) SerialPort.Write(abortSequence, 0, abortSequence.Length);
            }
        }

        // --- Receiving and Parsing ---
        private ResponseHeader ReadHeaderWithEchoFilter(EchoFilter ef, int timeoutMs, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var buffer = new List<byte>();

            while (!ct.IsCancellationRequested && sw.ElapsedMilliseconds < timeoutMs)
            {
                // First drain any bytes we've previously buffered
                lock (PadLock)
                {
                    while (_rxBuffer.Count > 0)
                    {
                        buffer.Add(_rxBuffer.Dequeue());
                    }
                }

                // Read anything new from serial
                int avail = 0;
                lock (PadLock) { avail = SerialPort.IsOpen ? SerialPort.BytesToRead : 0; }

                if (avail > 0)
                {
                    var tempBuf = new byte[avail];
                    lock (PadLock) { SerialPort.Read(tempBuf, 0, tempBuf.Length); }

                    int skipCount = ef.SkipEchoPrefix(tempBuf, 0, tempBuf.Length);
                    if (WithDebug && skipCount > 0) Console.WriteLine($"[ECHO] Skipped {skipCount} echo bytes.");

                    // enqueue remaining bytes into rx buffer
                    if (skipCount < tempBuf.Length)
                    {
                        for (int i = skipCount; i < tempBuf.Length; i++)
                        {
                            _rxBuffer.Enqueue(tempBuf[i]);
                        }
                        // transfer to working buffer for parsing
                        lock (PadLock)
                        {
                            while (_rxBuffer.Count > 0)
                                buffer.Add(_rxBuffer.Dequeue());
                        }
                    }
                }

                // Try to find headers in buffer
                // Look for hex header: ZPAD ZPAD ZDLE ZHEX ... (21 bytes)
                for (int i = 0; i <= buffer.Count - 5; i++)
                {
                    if (buffer[i] == (byte)ControlBytes.ZPAD && buffer[i + 1] == (byte)ControlBytes.ZPAD &&
                        buffer[i + 2] == (byte)ControlBytes.ZDLE)
                    {
                        // Check for HEX header
                        if (buffer[i + 3] == (byte)ControlBytes.ZHEX)
                        {
                            // full hex header length is typically 21 bytes
                            if (buffer.Count >= i + 21)
                            {
                                var headerBytes = buffer.Skip(i).Take(21).ToArray();
                                // consume
                                buffer.RemoveRange(0, i + 21);
                                return new ResponseHeader(headerBytes);
                            }
                        }
                        // Binary header: ZBIN (ZBIN is often 'A') or ZBIN32 ('C')
                        else if (buffer[i + 3] == (byte)ControlBytes.ZBIN || buffer[i + 3] == (byte)ControlBytes.ZBIN32)
                        {
                            // For binary headers we expect 7 bytes: ZPAD ZPAD ZDLE ZBIN <hdr-type><p0><p1><p2><p3><crc?> ...
                            // ResponseHeader should handle binary-form headers when provided with the proper sequence.
                            // We'll attempt to extract a minimal binary header length (10 bytes) if available.
                            if (buffer.Count >= i + 10)
                            {
                                // Pass the slice to ResponseHeader which should detect binary header type
                                // We take 10 bytes, ResponseHeader will read what it needs.
                                var headerBytes = buffer.Skip(i).Take(10).ToArray();
                                buffer.RemoveRange(0, i + 10);
                                return new ResponseHeader(headerBytes);
                            }
                        }
                    }
                }

                Thread.Sleep(20);
            }

            if (WithDebug) Console.WriteLine("ReadHeaderWithEchoFilter: timed out waiting for header.");
            return null; // Timeout
        }

        /// <summary>
        /// Receives a ZDLE-escaped data subpacket, decodes it, and validates CRC (auto-detect 2/4-byte CRC).
        /// Returns a DataSubpacket with data and end sequence; crcValid indicates whether CRC matched.
        /// </summary>
        private DataSubpacket ReceiveDataSubpacket(CRC32 crc32, EchoFilter ef, CancellationToken ct, out bool crcValid)
        {
            crcValid = false;
            var data = new List<byte>();
            ZDLESequence? endSequence = null;

            var sw = Stopwatch.StartNew();
            var inEscape = false;

            // 1. Read data until we encounter ZDLE + sequence (ZCRCE/ZCRCG/ZCRCQ/ZCRCW)
            while (endSequence == null && sw.ElapsedMilliseconds < HeaderWaitTimeoutMs && !ct.IsCancellationRequested)
            {
                int rb = ReadByteWithEchoFilter(ef, ct);
                if (rb == -1) { if (WithDebug) Console.WriteLine("[ERROR] Timeout in ReceiveDataSubpacket."); return null; }
                byte b = (byte)rb;

                if (inEscape)
                {
                    // Check if this byte is an end-of-data sequence
                    if (b == (byte)ZDLESequence.ZCRCE || b == (byte)ZDLESequence.ZCRCG || b == (byte)ZDLESequence.ZCRCQ || b == (byte)ZDLESequence.ZCRCW)
                    {
                        endSequence = (ZDLESequence)b;
                    }
                    else
                    {
                        // Not a sequence -> decode escape
                        if (b == (byte)ControlBytes.ZDLEE)
                            data.Add((byte)ControlBytes.ZDLE);
                        else
                            data.Add((byte)(b & ~0x40));
                    }
                    inEscape = false;
                }
                else if (b == (byte)ControlBytes.ZDLE)
                {
                    inEscape = true;
                }
                else
                {
                    data.Add(b);
                }
            }

            if (endSequence == null)
            {
                if (WithDebug) Console.WriteLine("[ERROR] Timeout or cancellation while reading data subpacket.");
                return null;
            }

            // 2. Read escaped CRC bytes. CRC may be 2 or 4 bytes depending on mode.
            // We'll read up to 4 decoded CRC bytes, but will accept 2 as CRC16 if only 2 are present.
            var crcDecoded = new List<byte>();
            var crcReadSw = Stopwatch.StartNew();
            while (crcDecoded.Count < 4 && crcReadSw.ElapsedMilliseconds < HeaderWaitTimeoutMs && !ct.IsCancellationRequested)
            {
                int rb = ReadByteWithEchoFilter(ef, ct);
                if (rb == -1)
                {
                    if (crcDecoded.Count >= 2)
                        break; // accept CRC16 if that's all we got
                    if (WithDebug) Console.WriteLine("[ERROR] Timeout reading CRC bytes.");
                    return null;
                }

                byte b = (byte)rb;
                if (b == (byte)ControlBytes.ZDLE)
                {
                    int r2 = ReadByteWithEchoFilter(ef, ct);
                    if (r2 == -1) { if (WithDebug) Console.WriteLine("[ERROR] Timeout reading escaped CRC byte."); return null; }
                    byte nxt = (byte)r2;
                    if (nxt == (byte)ControlBytes.ZDLEE)
                        crcDecoded.Add((byte)ControlBytes.ZDLE);
                    else
                        crcDecoded.Add((byte)(nxt & ~0x40));
                }
                else
                {
                    crcDecoded.Add(b);
                }

                // If we've read 2 bytes and the next byte attempts to start a header (ZPAD...), it's likely CRC16; break.
                if (crcDecoded.Count == 2)
                {
                    // Peek into rx buffer or serial to check if header follows; but to avoid blocking we accept 2 as valid for CRC16.
                    // The CRC mode negotiation should have indicated CRC32; nevertheless accept both.
                    // Continue loop to try to get 4 bytes if available.
                }
            }

            if (crcDecoded.Count < 2)
            {
                if (WithDebug) Console.WriteLine("[ERROR] CRC bytes incomplete.");
                return null;
            }

            // Compute CRC over data + endSequence
            // First try CRC32 if 4 bytes were provided; otherwise try CRC16.
            if (crcDecoded.Count >= 4)
            {
                var beforeCrc = data.Concat(new byte[] { (byte)endSequence.Value }).ToArray();
                var computed = crc32.ComputeHash(beforeCrc);
                crcValid = crcDecoded.Take(4).ToArray().SequenceEqual(computed);
                return new DataSubpacket { Data = data.ToArray(), FrameEnd = endSequence.Value };
            }
            else
            {
                // CRC16 path
                var crc16 = new CRC16();
                var beforeCrc = data.Concat(new byte[] { (byte)endSequence.Value }).ToArray();
                var computed16 = crc16.ComputeHash(beforeCrc);
                // computed16 is 2 bytes for CRC16 (assuming CRC16 ComputeHash returns 2 bytes)
                // But our CRC16.ComputeHash implementation might return 2 bytes; adapt accordingly.
                crcValid = crcDecoded.Take(2).ToArray().SequenceEqual(computed16);
                return new DataSubpacket { Data = data.ToArray(), FrameEnd = endSequence.Value };
            }
        }

        /// <summary>
        /// Read a single byte while honoring the echo filter and using the internal RX buffer.
        /// Returns -1 on timeout or cancellation.
        /// </summary>
        private int ReadByteWithEchoFilter(EchoFilter ef, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            // First, consume from internal rx buffer if any
            lock (PadLock)
            {
                if (_rxBuffer.Count > 0)
                {
                    var arr = _rxBuffer.ToArray();
                    int skip = ef.SkipEchoPrefix(arr, 0, arr.Length);
                    if (skip >= arr.Length)
                    {
                        // all were echo; remove them and continue to read
                        for (int i = 0; i < skip; i++) _rxBuffer.Dequeue();
                    }
                    else if (skip > 0)
                    {
                        for (int i = 0; i < skip; i++) _rxBuffer.Dequeue();
                        // return next byte
                        return _rxBuffer.Dequeue();
                    }
                    else
                    {
                        // No echo prefix - return first buffered byte
                        return _rxBuffer.Dequeue();
                    }
                }
            }

            while (!ct.IsCancellationRequested && sw.ElapsedMilliseconds < ReadByteTimeoutMs)
            {
                int avail;
                lock (PadLock) { avail = SerialPort.IsOpen ? SerialPort.BytesToRead : 0; }
                if (avail > 0)
                {
                    var buffer = new byte[avail];
                    lock (PadLock) { SerialPort.Read(buffer, 0, buffer.Length); }

                    // Apply echo filter to buffer. If it matches a prefix this is likely echo; skip those bytes.
                    int skip = ef.SkipEchoPrefix(buffer, 0, buffer.Length);
                    if (skip > 0)
                    {
                        if (skip < buffer.Length)
                        {
                            // buffer had some echo prefix, but there are remaining bytes which are real data.
                            // Enqueue remaining bytes into rx buffer and return next one.
                            lock (PadLock)
                            {
                                for (int i = skip; i < buffer.Length; i++) _rxBuffer.Enqueue(buffer[i]);
                            }
                            // return first byte from rxBuffer
                            lock (PadLock)
                            {
                                if (_rxBuffer.Count > 0) return _rxBuffer.Dequeue();
                            }
                        }
                        else
                        {
                            // Entire buffer was echo - continue loop (discarded)
                            continue;
                        }
                    }
                    else
                    {
                        // No echo prefix - enqueue all and return first
                        lock (PadLock)
                        {
                            for (int i = 0; i < buffer.Length; i++) _rxBuffer.Enqueue(buffer[i]);
                            if (_rxBuffer.Count > 0) return _rxBuffer.Dequeue();
                        }
                    }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }

            return -1; // timeout
        }

        private FileReceiveInfo ParseFileInformation(DataSubpacket dataPacket)
        {
            var text = Encoding.ASCII.GetString(dataPacket.Data).TrimEnd('\0');
            // filename\0filesize [octal mtime] or filename\0size SPACE octal_mtime
            // Keep original behavior: split on space but preserve filename until first null.
            var zeroIndex = Array.IndexOf(dataPacket.Data, (byte)0);
            string filename;
            string rest = null;
            if (zeroIndex >= 0)
            {
                filename = Encoding.ASCII.GetString(dataPacket.Data, 0, zeroIndex);
                if (zeroIndex + 1 < dataPacket.Data.Length)
                    rest = Encoding.ASCII.GetString(dataPacket.Data, zeroIndex + 1, dataPacket.Data.Length - zeroIndex - 1).Trim();
            }
            else
            {
                var parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                filename = parts[0];
                if (parts.Length > 1) rest = parts[1];
            }

            if (string.IsNullOrEmpty(filename)) return null;

            var fileInfo = new FileReceiveInfo { Name = Path.GetFileName(filename), Length = 0 };

            if (!string.IsNullOrEmpty(rest))
            {
                // If rest begins with digits then parse as decimal (size), possibly followed by space and octal time.
                var parts = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && long.TryParse(parts[0], out long len))
                {
                    fileInfo.Length = len;
                }
            }

            return fileInfo;
        }

        #endregion

        #region Helper Classes

        /// <summary>Stateful filter for removing command echo from the remote system.</summary>
        private class EchoFilter
        {
            private readonly Queue<byte> _recentSent = new Queue<byte>();
            private readonly object _lock = new object();
            private const int MaxHistory = 512;

            public void NoteSent(byte[] bytes)
            {
                if (bytes == null || bytes.Length == 0) return;
                lock (_lock)
                {
                    foreach (var b in bytes)
                    {
                        if (_recentSent.Count >= MaxHistory) _recentSent.Dequeue();
                        _recentSent.Enqueue(b);
                    }
                }
            }

            /// <summary>
            /// Attempt to find the longest prefix of 'incoming' that matches a contiguous sequence somewhere in recent sent history.
            /// Returns the count of leading bytes to skip (treat as echo). If no meaningful match found return 0.
            /// </summary>
            public int SkipEchoPrefix(byte[] incoming, int offset, int count)
            {
                lock (_lock)
                {
                    if (_recentSent.Count == 0 || incoming == null || count <= 0) return 0;

                    var sentArr = _recentSent.ToArray();
                    int bestMatch = 0;

                    // Try to align incoming[0] to every position in sentArr and measure longest match
                    for (int start = 0; start < sentArr.Length; start++)
                    {
                        int match = 0;
                        while (match < count && (start + match) < sentArr.Length && incoming[offset + match] == sentArr[start + match])
                        {
                            match++;
                        }
                        if (match > bestMatch) bestMatch = match;
                        // early exit: if we've matched all incoming bytes, that's maximal
                        if (bestMatch == count) break;
                    }

                    // To avoid false positives, require at least 2 bytes match
                    if (bestMatch >= 2)
                    {
                        // remove matched bytes from history
                        for (int i = 0; i < bestMatch && _recentSent.Count > 0; i++)
                            _recentSent.Dequeue();
                        return bestMatch;
                    }

                    return 0;
                }
            }
        }

        /// <summary>Information about a file being received.</summary>
        private class FileReceiveInfo
        {
            public string Name { get; set; }
            public long Length { get; set; }
        }

        /// <summary>Represents a received data subpacket.</summary>
        private class DataSubpacket
        {
            public byte[] Data { get; set; }
            public ZDLESequence FrameEnd { get; set; }
        }

        #endregion
    }
}
