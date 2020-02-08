using System;
using System.Diagnostics;
using System.Threading;
using System.Net.Sockets;
using IctBaden.Framework.Timer;
// ReSharper disable MemberCanBePrivate.Global

// ReSharper disable ConvertToUsingDeclaration

/*
 * Copyright 2012, 2013 by Mario Vernari, Cet Electronics
 * Part of "Cet Open Toolbox" (http://cetdevelop.codeplex.com/)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
// ReSharper disable once CheckNamespace
namespace Cet.IO.Net
{
    /// <summary>
    /// Implementation of a socket client (either TCP, or UDP)
    /// </summary>
    internal class IpClient
        : ICommClient
    {
        public IpClient(Socket port) => Port = port;


        public readonly Socket Port;

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public int Latency { get; set; }


        /// <summary>
        /// Entry-point for submitting a query to the remote device
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public CommResponse Query(ClientCommData data)
        {
            lock (Port)
            {
                //convert the request data as an ordinary byte array
                var outgoing = data.OutgoingData.ToByteArray();

                //create a writer for accumulate the incoming data
                var incoming = new ByteArrayWriter();

                const int tempSize = 1024;
                var temp = new byte[tempSize];

                //retries loop
                for (int attempt = 0, retries = data.Retries; attempt < retries; attempt++)
                {
                    //physical writing
                    try
                    {
                        Port.Send(outgoing);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);

                        return !Port.Connected 
                            ? new CommResponse(data, CommResponse.ConnectionLost) 
                            : new CommResponse(data, CommResponse.Critical);
                    }
                

                    incoming.Drop();

                    //start the local timer
                    var timeout = new PassiveTimer(Latency + data.Timeout);
                    //reception loop, until a valid response or timeout
                    while (!timeout.Timeout)
                    {
                        int length;
                        try
                        {
                            length = Port.Available;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                            length = 0;
                        }

                        if (length > 0)
                        {
                            if (length > tempSize)
                            {
                                Trace.TraceWarning("IpClient.Query: Receive length exceeds expected size.");
                                length = tempSize;
                            }

                            //read the incoming data from the physical port
                            Port.Receive(temp, length, SocketFlags.None);

                            //append data to the writer
                            incoming.WriteBytes(
                                temp,
                                0,
                                length);

                            //try to decode the stream
                            data.IncomingData = incoming.ToReader();

                            var result = data
                                .OwnerProtocol
                                .Codec
                                .ClientDecode(data);

                            //exit whether any concrete result: either good or bad
                            if (result.Status == CommResponse.Ack)
                            {
                                return result;
                            }

                            if (result.Status == CommResponse.Critical)
                            {
                                return result;
                            }

                            if (result.Status != CommResponse.Unknown)
                            {
                                break;
                            }
                        }

                        Thread.Sleep(0);
                    }
                } //for

                Trace.TraceError("IpClient:Query: no attempt was successful");
                return new CommResponse(data, CommResponse.Critical);
            } //lock
        }
    }
}