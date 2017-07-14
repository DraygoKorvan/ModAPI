using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.ModAPI;
using VRageMath;

namespace Draygo.AtmosphericPhysics.Settings
{
	public class RemoteDragSettings
	{
		private bool init = false;
		private bool m_Response = false;
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
				if (m_Response)
					return (bool)GetSetting((int)SettingsEnum.digi);
                return m_DigiPhysics;
			}
		}
		public bool AdvLift
		{
			get
			{
				if (!init)
					Register();
				if (m_Response)
					return (bool)GetSetting((int)SettingsEnum.advlift);
				return m_AdvLift;
			}
		}
		public static Func<int, object> GetSetting;
		public static Func<MyPlanet, Vector3D, IMyEntity, Vector3D> WindGetter;


		public Vector3D? GetWind(MyPlanet Planet, Vector3D pos , IMyEntity  ent)
		{
			if (WindGetter != null)
				return WindGetter(Planet, pos, ent);
			return null;
		}

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
			try
			{
				if (m_Response)
					return;
				if (obj is MyTuple<Func<int, object>, Func<MyPlanet, Vector3D, IMyEntity, Vector3D>>)
				{
					m_Response = true;
					var tupl = (MyTuple<Func<int, object>,Func<MyPlanet, Vector3D, IMyEntity, Vector3D>>)obj;
					GetSetting = tupl.Item1;
					WindGetter = tupl.Item2;

				}
			}
			catch
			{

			}

			
		}
	}
}
