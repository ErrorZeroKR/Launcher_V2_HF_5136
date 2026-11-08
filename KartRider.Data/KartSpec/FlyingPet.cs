using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using KartSpec;

namespace KartRider
{
	public class FlyingPet
	{
		// 게임 파일(aaa.pk)에서 읽어 온 펫 이름/스펙 (Rho.cs 에서 채움)
		public static Dictionary<int, string> flyingName = new Dictionary<int, string>();
		public static Dictionary<string, XmlDocument> flyingSpec = new Dictionary<string, XmlDocument>();

		public static float DragFactor;
		public static float ForwardAccelForce;
		public static float DriftEscapeForce;
		public static float CornerDrawFactor;
		public static float NormalBoosterTime;
		public static float ItemBoosterTime;
		public static float TeamBoosterTime;
		public static float StartForwardAccelForceItem;
		public static float StartForwardAccelForceSpeed;

		private static float Attr(XmlElement e, params string[] names)
		{
			foreach (string n in names)
				if (float.TryParse(e.GetAttribute(n), NumberStyles.Any, CultureInfo.InvariantCulture, out float v)) return v;
			return 0f;
		}

		/// <summary>게임 파일에서 읽은 펫 스펙 적용 (P3563 FlyingPet.cs 와 같은 속성명 사용)</summary>
		public static bool TryApplyGameData(int id)
		{
			if (!KartRhoFile.Loaded || id == 0) return false;
			if (!flyingName.TryGetValue(id, out string name) || !flyingSpec.TryGetValue(name, out XmlDocument doc)) return false;
			XmlElement e = doc.DocumentElement;
			if (e == null) return false;
			DragFactor = Attr(e, "DragFactor");
			ForwardAccelForce = Attr(e, "ForwardAccelForce");
			DriftEscapeForce = Attr(e, "DriftEscapeForce");
			CornerDrawFactor = Attr(e, "CornerDrawFactor");
			NormalBoosterTime = Attr(e, "NormalBoosterTime");
			ItemBoosterTime = Attr(e, "ItemBoosterTime");
			TeamBoosterTime = Attr(e, "TeamBoosterTime");
			StartForwardAccelForceItem = Attr(e, "StartForwardAccelItem", "StartForwardAccelForceItem");
			StartForwardAccelForceSpeed = Attr(e, "StartForwardAccelSpeed", "StartForwardAccelForceSpeed");
			Console.WriteLine("[FlyingPet] 게임 파일 스펙 적용: id={0}, name={1}", id, name);
			return true;
		}

		private static void ZeroAll()
		{
			DragFactor = 0f;
			ForwardAccelForce = 0f;
			DriftEscapeForce = 0f;
			CornerDrawFactor = 0f;
			NormalBoosterTime = 0f;
			ItemBoosterTime = 0f;
			TeamBoosterTime = 0f;
			StartForwardAccelForceItem = 0f;
			StartForwardAccelForceSpeed = 0f;
		}

		/// <summary>펫 성능도 항상 게임 데이터에서 읽음 (펫이 없거나 게임 데이터에 없으면 0)</summary>
		public static void FlyingPet_Spec()
		{
			// 폴더 방식(datapackOff)이면 이번 카트/펫의 param 파일을 다시 읽어 게임 실행 중 수정한 값도 반영
			KartRhoFile.ReloadForRace(StartGameData.Kart_id, StartGameData.FlyingPet_id);
			int id = StartGameData.FlyingPet_id;
			if (id == 0)
			{
				ZeroAll();
			}
			else if (!TryApplyGameData(id))
			{
				ZeroAll();
				Console.WriteLine("[FlyingPet] 게임 데이터에 펫 id={0} 의 스펙이 없어 펫 성능을 적용하지 않아요", id);
			}
			Kart_Spec.KartAll();
		}
	}
}
