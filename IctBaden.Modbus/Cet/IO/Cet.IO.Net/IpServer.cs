using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cet.IO.Protocols;
// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming

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
namespace Cet.IO.Net
{
    /// <summary>
    /// Base/abstract implementation of an IP-listener
    /// </summary>
    public abstract class IpServer
        : ICommServer
    {
        protected IpServer(
            Socket port,
            IProtocol protocol)
        {
            this.Port = port;
            this.Protocol = protocol;
        }


        public readonly Socket Port;
        public readonly IProtocol Protocol;

        private Task _task;
        protected bool _closing;


        /// <summary>
        /// Indicate whether the server is running
        /// </summary>
        public bool IsRunning { get; protected set; }


        /// <summary>
        /// Start the listener session
        /// </summary>
        public void Start()
        {
            //marks the server running
            this.IsRunning = true;

            this._task = new Task(this.Worker);
            this._task.Start();
        }


        /// <summary>
        /// Handler for the session thread
        /// </summary>
        protected abstract void Worker();


        /// <summary>
        /// Close/abort the listener session
        /// </summary>
        public void Abort()
        {
            this._closing = true;

            if (this._task != null && !this._task.IsCompleted)
            {
                this._task.Wait();
            }
        }


        #region EVT ServeCommand

        public event ServeCommandHandler ServeCommand;


        protected virtual void OnServeCommand(ServerCommData data)
        {
            try
            {
                ServeCommand?.Invoke(this, new ServeCommandEventArgs(data));
            }
            catch (Exception ex)
            {
                Trace.TraceError("OnServeCommand: " + ex.Message);
            }
        }

        #endregion

    }
}
