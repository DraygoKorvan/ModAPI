using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game;
using ProtoBuf;

//............................................
//Created by DraygoKorvan
//Free to use and modify as you see fit. 
//Credit is not required on your Workshop page
//Please leave this comment block intact
//Purpose of this class is to prevent other players from 
// spoofing commands to a server while pretending they are someone else. 
//This is to be used to protect remote admin commands and other sensitive commands. 
//File created 18/06/2020
//Version 1.0
//............................................

namespace Draygo.SecureChannel
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class ModAPISecureChannel : MySessionComponentBase
    {
        //its fine to change these
        const ushort SECURECHANNEL_ID = 443;
        const ushort UNSECURECHANNEL_ID = 80;

        //Length can be changed. Should work fine. 
        byte[] selfkey = new byte[4];

        //you might want to leave the code below here alone, up to you ;)
        Dictionary<ulong, byte[]> encryptlookuptable = new Dictionary<ulong, byte[]>();
        public static ModAPISecureChannel instance;
        Dictionary<ushort, Action<ulong, byte[]>> secureMessageHandlers = new Dictionary<ushort, Action<ulong, byte[]>>();
        Dictionary<ushort, Action<ulong, byte[]>> messageHandlers = new Dictionary<ushort, Action<ulong, byte[]>>();
        ulong modid = 0;

        Random rgen;
        private List<IMyPlayer> idents = new List<IMyPlayer>();
        private bool m_registered = false;

        [ProtoContract]
        public struct SecureMessage
        {
            [ProtoMember(1)]
            public ulong steamid;
            [ProtoMember(2)]
            public byte[] data;
        }

        [ProtoContract]
        public struct MessageData
        {
            [ProtoMember(1)]
            public ulong modid;
            [ProtoMember(2)]
            public ushort messageid;
            [ProtoMember(3)]
            public byte[] message;
        }

        public ModAPISecureChannel()
        {
            instance = this;
        }

        private void receiveMessage(byte[] obj)
        {
            try
            {
                var message = MyAPIGateway.Utilities.SerializeFromBinary<SecureMessage>(obj);

                var data = MyAPIGateway.Utilities.SerializeFromBinary<MessageData>(message.data);
                if (data.modid != modid)
                {
                    return;
                }
                if (data.messageid == 0)
                {
                    if (MyAPIGateway.Session.IsServer)
                    {
                        //add key from client to dictionary. 
                        if (encryptlookuptable.ContainsKey(message.steamid))
                        {
                            encryptlookuptable.Remove(message.steamid);
                        }
                        rgen.NextBytes(selfkey);

                        byte[] mdata = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
                        {
                            modid = this.modid,
                            messageid = 0,
                            message = selfkey
                        });

                        EncryptDecrypt(ref mdata, ref data.message);

                        var hello = new SecureMessage()
                        {
                            steamid = 0,
                            data = mdata
                        };

                        //we could send this secure?
                        MyAPIGateway.Multiplayer.SendMessageTo(SECURECHANNEL_ID, MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(hello), message.steamid);
                        EncryptDecrypt(ref data.message, ref selfkey);
                        encryptlookuptable.Add(message.steamid, data.message);
                    }

                    return;
                }
                Action<ulong, byte[]> handle;
                if (messageHandlers.TryGetValue(data.messageid, out handle))
                {
                    handle(message.steamid, data.message);
                }
            }
            catch
            {
                //do nothing
            }
        }

        private void receiveSecureMessage(byte[] obj)
        {
            try
            {
                var message = MyAPIGateway.Utilities.SerializeFromBinary<SecureMessage>(obj);
                if (!TryEncryptDecrypt(ref message.data, message.steamid))
                {
                    return;
                }
                var data = MyAPIGateway.Utilities.SerializeFromBinary<MessageData>(message.data);
                if (data.modid != modid)
                {
                    return;
                }
                if (data.messageid == 0)
                {
                    if (!MyAPIGateway.Session.IsServer)
                    {
                        if (message.steamid == 0)
                        {
                            EncryptDecrypt(ref selfkey, ref data.message);
                        }
                    }
                    return;
                }
                Action<ulong, byte[]> handle;
                if (secureMessageHandlers.TryGetValue(data.messageid, out handle))
                {
                    handle(message.steamid, data.message);
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// Message Handler for Secure Messages, calls messageHandler with the senders steamid and data. ID needs to be unique otherwise it will fail.
        /// </summary>
        /// <param name="messageid">Cannot be 0, must be unique</param>
        /// <param name="MessageHandler">calls a method with ulong SteamID and the byte[] MessageData as its parameters.</param>
        public void RegisterSecureMessageHandler(ushort messageid, Action<ulong, byte[]> MessageHandler)
        {
            if (messageid == 0)
            {
                return;//do not register 0
            }
            if (secureMessageHandlers.ContainsKey(messageid))
            {
                return;//do not register
            }
            secureMessageHandlers.Add(messageid, MessageHandler);
        }

        /// <summary>
        /// Removes secure message handler. Do not call in or after UnloadData. This class will dereference itself. 
        /// </summary>
        /// <param name="messageid"></param>
        public void UnRegisterSecureMessageHandler(ushort messageid)
        {
            if (secureMessageHandlers.ContainsKey(messageid))
            {
                secureMessageHandlers.Remove(messageid);
            }
        }

        /// <summary>
        /// Adds a message handler, keep in mind that other mods will be able to read and potentially intepret this data. 
        /// </summary>
        /// <param name="messageid">Cannot be 0, must be unique</param>
        /// <param name="MessageHandler">calls a method with ulong SteamID and the byte[] MessageData as its parameters, please note that steamid is not validated and should not be trusted for any privileged operation</param>
        public void RegisterMessageHandler(ushort messageid, Action<ulong, byte[]> MessageHandler)
        {
            if (messageid == 0)
            {
                return;//do not register 0
            }
            if (messageHandlers.ContainsKey(messageid))
            {
                return;//do not register
            }
            messageHandlers.Add(messageid, MessageHandler);
        }

        /// <summary>
        /// Unregisters message handler using messageid.  Do not call in or after UnloadData. This class will dereference itself. 
        /// </summary>
        /// <param name="messageid">Unique message id</param>
        public void UnRegisterMessageHandler(ushort messageid)
        {
            if (messageHandlers.ContainsKey(messageid))
            {
                messageHandlers.Remove(messageid);
            }
        }

        /// <summary>
        /// Send a message using the secure channel to the server. Client to Server Only.  
        /// </summary>
        /// <param name="messageId">Each Mod maintains its own list of messageId's. You cannot send data to other mods using this method.</param>
        /// <param name="data">message</param>
        /// <param name="reliable">Optional, send reliably</param>
        public void SendSecureMessageToServer(ushort messageId, byte[] data, bool reliable = true)
        {
            var edata = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
            {
                modid = this.modid,
                messageid = messageId,
                message = data
            });

            TryEncryptDecrypt(ref edata, MyAPIGateway.Session.Player.SteamUserId);

            var sdata = MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(new SecureMessage()
            {
                steamid = MyAPIGateway.Session.Player.SteamUserId,
                data = edata
            });

            MyAPIGateway.Multiplayer.SendMessageToServer(SECURECHANNEL_ID, sdata, reliable);
        }

        /// <summary>
        /// Server sending to all Players, Server Only. 
        /// </summary>
        /// <param name="messageId">Each Mod maintains its own list of messageId's. You cannot send data to other mods using this method.</param>
        /// <param name="data">message</param>
        /// <param name="reliable">Optional, send reliably</param>
        public void SendSecureMessageToOthers(ushort messageId, byte[] data, bool reliable = true)
        {

            var udata = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
            {
                modid = this.modid,
                messageid = messageId,
                message = data
            });

            var players = MyAPIGateway.Multiplayer.Players;
            idents.Clear();
            players.GetPlayers(idents);
            foreach (var ident in idents)
            {
                if (ident.IsBot)
                    continue;
                if (ident.SteamUserId == 0)
                    continue;
                var edata = data;
                TryEncryptDecrypt(ref edata, ident.SteamUserId);

                var sdata = MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(new SecureMessage()
                {
                    steamid = ident.SteamUserId,
                    data = edata
                });

                MyAPIGateway.Multiplayer.SendMessageTo(SECURECHANNEL_ID, sdata, ident.SteamUserId, reliable);
            }
        }

        /// <summary>
        /// Server to a single Client
        /// </summary>
        /// <param name="messageId">Each Mod maintains its own list of messageId's. You cannot send data to other mods using this method.</param>
        /// <param name="data">message</param>
        /// <param name="recipient">steamid of user</param>
        /// <param name="reliable">Optional, send reliably</param>
        public void SendSecureMessageTo(ushort messageId, byte[] data, ulong recipient, bool reliable = true)
        {

            var edata = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
            {
                modid = this.modid,
                messageid = messageId,
                message = data
            });

			TryEncryptDecrypt(ref edata, recipient);

            var sdata = MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(new SecureMessage()
            {
                steamid = recipient,
                data = edata
            });

            MyAPIGateway.Multiplayer.SendMessageTo(SECURECHANNEL_ID, sdata, recipient, reliable);
        }

        /// <summary>
        /// Client to Server. Not secure can be spoofed. Do not use this for privileged traffic. 
        /// </summary>
        /// <param name="messageId">Each Mod maintains its own list of messageId's. You cannot send data to other mods using this method.</param>
        /// <param name="data">message</param>
        /// <param name="reliable">Optional, send reliably</param>
        public void SendMessageToServer(ushort messageId, byte[] data, bool reliable = true)
        {

            var sdata = MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(new SecureMessage()
            {
                steamid = MyAPIGateway.Session.Player.SteamUserId,
                data = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
                {
                    modid = this.modid,
                    messageid = messageId,
                    message = data
                })
            });
            MyAPIGateway.Multiplayer.SendMessageToServer(UNSECURECHANNEL_ID, sdata, reliable);
        }

        /// <summary>
        /// Server to all connected clients. Not secure and can be spoofed, do not use for privileged traffic.
        /// </summary>
        /// <param name="messageId">Each Mod maintains its own list of messageId's. You cannot send data to other mods using this method.</param>
        /// <param name="data">message</param>
        /// <param name="reliable">Optional, send reliably</param>
        public void SendMessageToOthers(ushort messageId, byte[] data, bool reliable = true)
        {

            var sdata = MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(new SecureMessage()
            {
                steamid = MyAPIGateway.Session.Player.SteamUserId,
                data = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
                {
                    modid = this.modid,
                    messageid = messageId,
                    message = data
                })
            });
            MyAPIGateway.Multiplayer.SendMessageToOthers(UNSECURECHANNEL_ID, sdata, reliable);
        }

        /// <summary>
        /// Server to a single client. Not secure and can be spoofed, do not use for privileged traffic.
        /// </summary>
        /// <param name="messageId">Each Mod maintains its own list of messageId's. You cannot send data to other mods using this method.</param>
        /// <param name="data">message</param>
        /// <param name="recipient">steamid of client</param>
        /// <param name="reliable">Optional, send reliably</param>
        public void SendMessageTo(ushort messageId, byte[] data, ulong recipient, bool reliable = true)
        {

            var sdata = MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(new SecureMessage()
            {
                steamid = recipient,
                data = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
                {
                    modid = this.modid,
                    messageid = messageId,
                    message = data
                })
            });
            MyAPIGateway.Multiplayer.SendMessageTo(UNSECURECHANNEL_ID, sdata, recipient, reliable);
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {

            base.Init(sessionComponent);

            modid = ulong.Parse(this.ModContext.ModId.Substring(0, this.ModContext.ModId.Length - 4));
            if (MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                m_registered = true;
                MyAPIGateway.Multiplayer.RegisterMessageHandler(SECURECHANNEL_ID, receiveSecureMessage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(UNSECURECHANNEL_ID, receiveMessage);
                rgen = new Random();
                if (!MyAPIGateway.Session.IsServer)
                {


                    rgen.NextBytes(selfkey);

                    var hello = new SecureMessage()
                    {
                        steamid = MyAPIGateway.Multiplayer.MyId,
                        data = MyAPIGateway.Utilities.SerializeToBinary<MessageData>(new MessageData()
                        {
                            modid = this.modid,
                            messageid = 0,
                            message = selfkey
                        })
                    };
                    MyAPIGateway.Multiplayer.SendMessageToServer(UNSECURECHANNEL_ID, MyAPIGateway.Utilities.SerializeToBinary<SecureMessage>(hello));
                }
            }
        }

        protected override void UnloadData()
        {
            if (m_registered)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(SECURECHANNEL_ID, receiveSecureMessage);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(UNSECURECHANNEL_ID, receiveMessage);
            }
            instance = null;
        }


        private void EncryptDecrypt(ref byte[] szPlainText, ref byte[] szEncryptionKey)
        {
            for (int iCount = 0, step = 0; iCount < szPlainText.Length; iCount++, step++)
            {
                if (step == szEncryptionKey.Length)
                    step = 0;
                szPlainText[iCount] = (byte)(szPlainText[iCount] ^ szEncryptionKey[step]);
            }
        }

        private bool TryEncryptDecrypt(ref byte[] szPlainText, ulong steamid)
        {
            byte[] szEncryptionKey;
            if (MyAPIGateway.Session.IsServer)
            {
                if (encryptlookuptable.TryGetValue(steamid, out szEncryptionKey))
                {
                    EncryptDecrypt(ref szPlainText, ref szEncryptionKey);
                    return true;
                }
                return false;
            }
            else
            {
                EncryptDecrypt(ref szPlainText, ref selfkey);
                return true;
            }
        }
    }
}
