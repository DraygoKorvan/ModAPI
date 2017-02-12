using Sandbox.ModAPI;
using System;
using System.Collections.Generic;

namespace Draygo.AtmosphericPhysics.Settings
{
	public class RemoteDragSettings
	{
		private bool init = false;
		public const long MODID = 571920453;//mod ID of the Aerodynamic Physics mod this is the mod we want to talk to. 
		private const int GETSETTINGS = 1;

		private bool m_DigiPhysics = true;//keep digi physics enabled if mod is not installed. 
		private bool m_AdvLift = false;
		private enum SettingsEnum : int
		{
			digi = 0,
			advlift,
			show_warning,
			showburn,
			showsmoke
		}
		public bool DigiPhysics
		{
			get
			{
				if (!init)
					Register();
				return m_DigiPhysics;
			}
		}
		public bool AdvLift
		{
			get
			{
				if (!init)
					Register();
				return m_AdvLift;
			}
		}
		public static Action OnDigiPhysicsChanged;
		public static Action OnAdvLiftChanged;
		public void Register()
		{
			if(init)
			{
				MyAPIGateway.Utilities.UnregisterMessageHandler(MODID, Handler);//clear if already registered. 
			}
			init = true;
			MyAPIGateway.Utilities.RegisterMessageHandler(MODID, Handler);
			MyAPIGateway.Utilities.SendModMessage(MODID, GETSETTINGS);
			
        }
		public void UnRegister()
		{
			if (init)
			{
				MyAPIGateway.Utilities.UnregisterMessageHandler(MODID, Handler);
			}
			init = false;
		}
		private void Handler(object obj)
		{
			
			if (obj is int)
			{
				return;
			}
			try
			{
				Dictionary<int, bool> Response = (Dictionary<int, bool>)obj;
				bool _ret;
				if (Response.TryGetValue((int)SettingsEnum.digi, out _ret))
				{
					
					if (m_DigiPhysics != _ret)
					{
						m_DigiPhysics = _ret;
						if (OnDigiPhysicsChanged != null)
							OnDigiPhysicsChanged();
					}

				}
				if (Response.TryGetValue((int)SettingsEnum.advlift, out _ret))
				{
					
					if (_ret != m_AdvLift)
					{
						m_AdvLift = _ret;
						if (OnAdvLiftChanged != null)
							OnAdvLiftChanged();
					}

				}
			}
			catch
			{

			}

			
		}
	}
}
