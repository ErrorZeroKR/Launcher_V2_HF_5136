using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using KartRider;

namespace KartSpec
{
	/// <summary>
	/// 카트 성능은 항상 클라이언트(게임 데이터 aaa.pk 의 kart_/&lt;이름&gt;/param)에서 읽어옴.
	/// 런처 안에 저장해 둔 카트 스펙 표는 없음 (게임 데이터에 없는 카트는 기본 성능으로 처리).
	/// </summary>
	public class Kart_Spec
	{
		public static void KartAll()
		{
			int id = StartGameData.Kart_id;
			if (!TryApplyGameData(id))
			{
				// 게임 데이터에 없는 카트: 이전 카트의 값이 남지 않도록 기본 성능(표준 속도 기본값)으로 채움
				ApplyDefaults();
				if (id != 0) Console.WriteLine("[KartSpec] 게임 데이터에 카트 id={0} 의 스펙이 없어 기본 성능을 사용해요", id);
			}
			StartGameData.Start_KartSpac();
		}

		// ---- 게임 파일(aaa.pk)의 BodyParam 을 Kart.* 에 적용 (P3563/V2 KartSpec.cs 규칙: 값*scale+fallback) ----
		private sealed class SpecRow
		{
			public string Attr; public decimal Fallback, Default, Scale; public Action<decimal> Set;
			public SpecRow(string a, decimal f, decimal d, decimal s, Action<decimal> set) { Attr = a; Fallback = f; Default = d; Scale = s; Set = set; }
		}

		private static readonly HashSet<string> BoolAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"UseTransformBooster", "motorcycleType", "BikeRearWheel", "UseExtendedAfterBooster", "dualBoosterSetAuto", "PartsEngineLock", "PartsWheelLock", "PartsSteeringLock", "PartsBoosterLock", "PartsCoatingLock", "PartsTailLampLock", "useExtendedAfterBoosterMore"
		};

		private static readonly List<SpecRow> SpecRows = new List<SpecRow>
		{
			new SpecRow("draftMulAccelFactor", 0M, 1M, 1M, val => Kart.draftMulAccelFactor = (float)val),
			new SpecRow("draftTick", 0M, 0M, 1M, val => Kart.draftTick = (int)val),
			new SpecRow("driftBoostMulAccelFactor", 0M, 1M, 1M, val => Kart.driftBoostMulAccelFactor = (float)val),
			new SpecRow("driftBoostTick", 0M, 0M, 1M, val => Kart.driftBoostTick = (int)val),
			new SpecRow("chargeBoostBySpeed", 0M, 0M, 1M, val => Kart.chargeBoostBySpeed = (float)val),
			new SpecRow("SpeedSlotCapacity", 0M, 2M, 1M, val => Kart.SpeedSlotCapacity = (byte)val),
			new SpecRow("ItemSlotCapacity", 0M, 2M, 1M, val => Kart.ItemSlotCapacity = (byte)val),
			new SpecRow("SpecialSlotCapacity", 0M, 1M, 1M, val => Kart.SpecialSlotCapacity = (byte)val),
			new SpecRow("UseTransformBooster", 0M, 0M, 1M, val => Kart.UseTransformBooster = (byte)val),
			new SpecRow("motorcycleType", 0M, 0M, 1M, val => Kart.motorcycleType = (byte)val),
			new SpecRow("BikeRearWheel", 0M, 1M, 1M, val => Kart.BikeRearWheel = (byte)val),
			new SpecRow("Mass", 0M, 0M, 1M, val => Kart.Mass = (float)val),
			new SpecRow("AirFriction", 0M, 0M, 1M, val => Kart.AirFriction = (float)val),
			new SpecRow("DragFactor", 0M, 0M, 1M, val => Kart.DragFactor = (float)val),
			new SpecRow("ForwardAccelForce", 0M, 0M, 1M, val => Kart.ForwardAccelForce = (float)val),
			new SpecRow("BackwardAccelForce", 0M, 0M, 1M, val => Kart.BackwardAccelForce = (float)val),
			new SpecRow("GripBrakeForce", 0M, 0M, 1M, val => Kart.GripBrakeForce = (float)val),
			new SpecRow("SlipBrakeForce", 0M, 0M, 1M, val => Kart.SlipBrakeForce = (float)val),
			new SpecRow("MaxSteerAngle", 0M, 0M, 1M, val => Kart.MaxSteerAngle = (float)val),
			new SpecRow("SteerConstraint", 0M, 0M, 1M, val => Kart.SteerConstraint = (float)val),
			new SpecRow("FrontGripFactor", 0M, 0M, 1M, val => Kart.FrontGripFactor = (float)val),
			new SpecRow("RearGripFactor", 0M, 0M, 1M, val => Kart.RearGripFactor = (float)val),
			new SpecRow("DriftTriggerFactor", 0M, 0M, 1M, val => Kart.DriftTriggerFactor = (float)val),
			new SpecRow("DriftTriggerTime", 0M, 0M, 1M, val => Kart.DriftTriggerTime = (float)val),
			new SpecRow("DriftSlipFactor", 0M, 0M, 1M, val => Kart.DriftSlipFactor = (float)val),
			new SpecRow("DriftEscapeForce", 0M, 0M, 1M, val => Kart.DriftEscapeForce = (float)val),
			new SpecRow("CornerDrawFactor", 0M, 0M, 1M, val => Kart.CornerDrawFactor = (float)val),
			new SpecRow("DriftLeanFactor", 0.07M, 0.07M, 1M, val => Kart.DriftLeanFactor = (float)val),
			new SpecRow("SteerLeanFactor", 0.01M, 0.01M, 1M, val => Kart.SteerLeanFactor = (float)val),
			new SpecRow("DriftMaxGauge", 0M, 0M, 1M, val => Kart.DriftMaxGauge = (float)val),
			new SpecRow("NormalBoosterTime", 3000M, 3000M, 1M, val => Kart.NormalBoosterTime = (float)val),
			new SpecRow("ItemBoosterTime", 3000M, 3000M, 1M, val => Kart.ItemBoosterTime = (float)val),
			new SpecRow("TeamBoosterTime", 4500M, 4500M, 1M, val => Kart.TeamBoosterTime = (float)val),
			new SpecRow("AnimalBoosterTime", 4000M, 4000M, 1M, val => Kart.AnimalBoosterTime = (float)val),
			new SpecRow("SuperBoosterTime", 3500M, 3500M, 1M, val => Kart.SuperBoosterTime = (float)val),
			new SpecRow("TransAccelFactor", 0M, 1.5M, 1M, val => Kart.TransAccelFactor = (float)val),
			new SpecRow("BoostAccelFactor", 0M, 1.5M, 1M, val => Kart.BoostAccelFactor = (float)val),
			new SpecRow("StartBoosterTimeItem", 0M, 1000M, 1M, val => Kart.StartBoosterTimeItem = (float)val),
			new SpecRow("StartBoosterTimeSpeed", 0M, 1000M, 1M, val => Kart.StartBoosterTimeSpeed = (float)val),
			new SpecRow("StartForwardAccelFactorItem", 0M, 0M, 1M, val => Kart.StartForwardAccelForceItem = (float)val),
			new SpecRow("StartForwardAccelFactorSpeed", 0M, 0M, 1M, val => Kart.StartForwardAccelForceSpeed = (float)val),
			new SpecRow("DriftGaguePreservePercent", 0M, 0M, 1M, val => Kart.DriftGaguePreservePercent = (float)val),
			new SpecRow("UseExtendedAfterBooster", 0M, 0M, 1M, val => Kart.UseExtendedAfterBooster = (byte)val),
			new SpecRow("BoostAccelFactorOnlyItem", 0M, 1.5M, 1M, val => Kart.BoostAccelFactorOnlyItem = (float)val),
			new SpecRow("antiCollideBalance", 0M, 1M, 1M, val => Kart.antiCollideBalance = (float)val),
			new SpecRow("dualBoosterSetAuto", 0M, 0M, 1M, val => Kart.dualBoosterSetAuto = (byte)val),
			new SpecRow("dualBoosterTickMin", 0M, 40M, 1M, val => Kart.dualBoosterTickMin = (int)val),
			new SpecRow("dualBoosterTickMax", 0M, 60M, 1M, val => Kart.dualBoosterTickMax = (int)val),
			new SpecRow("dualMulAccelFactor", 0M, 1.1M, 1M, val => Kart.dualMulAccelFactor = (float)val),
			new SpecRow("dualTransLowSpeed", 0M, 100M, 1M, val => Kart.dualTransLowSpeed = (float)val),
			new SpecRow("PartsEngineLock", 0M, 0M, 1M, val => Kart.PartsEngineLock = (byte)val),
			new SpecRow("PartsWheelLock", 0M, 0M, 1M, val => Kart.PartsWheelLock = (byte)val),
			new SpecRow("PartsSteeringLock", 0M, 0M, 1M, val => Kart.PartsSteeringLock = (byte)val),
			new SpecRow("PartsBoosterLock", 0M, 0M, 1M, val => Kart.PartsBoosterLock = (byte)val),
			new SpecRow("PartsCoatingLock", 0M, 0M, 1M, val => Kart.PartsCoatingLock = (byte)val),
			new SpecRow("PartsTailLampLock", 0M, 0M, 1M, val => Kart.PartsTailLampLock = (byte)val),
			new SpecRow("chargeInstAccelGaugeByBoost", 0M, 0.02M, 1M, val => Kart.chargeInstAccelGaugeByBoost = (float)val),
			new SpecRow("chargeInstAccelGaugeByGrip", 0M, 0.02M, 1M, val => Kart.chargeInstAccelGaugeByGrip = (float)val),
			new SpecRow("chargeInstAccelGaugeByWall", 0M, 0.2M, 1M, val => Kart.chargeInstAccelGaugeByWall = (float)val),
			new SpecRow("instAccelFactor", 0M, 1.25M, 1M, val => Kart.instAccelFactor = (float)val),
			new SpecRow("instAccelGaugeCooldownTime", 0M, 0M, 1000M, val => Kart.instAccelGaugeCooldownTime = (int)val),
			new SpecRow("instAccelGaugeLength", 0M, 0M, 1000M, val => Kart.instAccelGaugeLength = (float)val),
			new SpecRow("instAccelGaugeMinUsable", 0M, 0M, 1000M, val => Kart.instAccelGaugeMinUsable = (float)val),
			new SpecRow("instAccelGaugeMinVelBound", 0M, 200M, 1M, val => Kart.instAccelGaugeMinVelBound = (float)val),
			new SpecRow("instAccelGaugeMinVelLoss", 0M, 50M, 1M, val => Kart.instAccelGaugeMinVelLoss = (float)val),
			new SpecRow("useExtendedAfterBoosterMore", 0M, 0M, 1M, val => Kart.useExtendedAfterBoosterMore = (byte)val),
			new SpecRow("wallCollGaugeCooldownTime", 0M, 0M, 1000M, val => Kart.wallCollGaugeCooldownTime = (int)val),
			new SpecRow("wallCollGaugeMaxVelLoss", 0M, 200M, 1M, val => Kart.wallCollGaugeMaxVelLoss = (float)val),
			new SpecRow("wallCollGaugeMinVelBound", 0M, 200M, 1M, val => Kart.wallCollGaugeMinVelBound = (float)val),
			new SpecRow("wallCollGaugeMinVelLoss", 0M, 50M, 1M, val => Kart.wallCollGaugeMinVelLoss = (float)val),
		};

		/// <summary>게임 데이터의 BodyParam(없으면 모두 기본값)을 읽어 Kart.* 에 (기본값 보정 전의) 원본 값으로 채움</summary>
		private static void ApplyBody(XmlElement body)
		{
			foreach (SpecRow r in SpecRows)
			{
				string s = body != null ? body.GetAttribute(r.Attr) : "";
				decimal v;
				if (BoolAttrs.Contains(r.Attr))
				{
					if (bool.TryParse(s, out bool b)) v = b ? 1M : 0M;
					else if (s == "1" || s == "0") v = s == "1" ? 1M : 0M;
					else v = r.Default;
				}
				else
				{
					decimal scale = r.Scale;
					if (r.Attr == "instAccelGaugeMinUsable")
						scale *= decimal.TryParse(body != null ? body.GetAttribute("instAccelGaugeLength") : "", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal len) ? len : 0M;
					v = decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pv) ? pv * scale + r.Fallback : r.Default;
				}
				r.Set(v);
			}
		}

		private static bool FindBody(int kartId, out KartTable table, out XmlElement body)
		{
			table = null;
			body = null;
			if (!Kart.kartName.TryGetValue(kartId, out table) || table == null) return false;
			if (!Kart.kartSpec.TryGetValue(table.Name, out XmlDocument doc)) return false;
			foreach (XmlElement e in doc.GetElementsByTagName("BodyParam")) { body = e; break; }
			return body != null;
		}

		/// <summary>게임 데이터에서 읽은 스펙을 Kart.* 에 적용하고 true. 게임 데이터에 없으면 false.</summary>
		public static bool TryApplyGameData(int kartId)
		{
			if (!KartRhoFile.Loaded || kartId == 0) return false;
			KartTable table = null;
			XmlElement body = null;
			bool found;
			lock (KartRhoFile.DataLock) { found = FindBody(kartId, out table, out body); }
			if (!found) return false;
			ApplyBody(body);
			string notes = ApplyStandardBase();
			Console.WriteLine("[KartSpec] 게임 데이터 스펙 적용: id={0}, name={1} (표준 속도 기본값 보정 포함{2})", kartId, table.Name, notes);
			return true;
		}

		/// <summary>스펙이 없는 카트용 기본 성능 (표준 속도 기본값만)</summary>
		private static void ApplyDefaults()
		{
			ApplyBody(null);
			ApplyStandardBase();
		}

		/// <summary>
		/// 게임 파일의 카트 값은 '표준 속도 기본값 대비 차이'만 담고 있음 (P3563/V2 는 속도 종류별 기본값을 더해서 사용).
		/// HF 내장 표(Profile\KartSpec.xml)는 '기본값 + 차이'가 이미 합쳐진 값이라서, 같은 형태가 되도록 표준 속도 기본값(P3563 Default())을 더함.
		/// 다른 속도(S0~S3 등)는 HF 의 SpeedType 오프셋이 그대로 추가로 적용됨.
		/// </summary>
		private static string ApplyStandardBase()
		{
			string notes = "";
			Kart.Mass += 100f;
			Kart.AirFriction += 3f;
			Kart.DragFactor += 0.75f;
			Kart.ForwardAccelForce += 2150f;
			Kart.BackwardAccelForce += 1725f;
			Kart.GripBrakeForce += 2070f;
			Kart.SlipBrakeForce += 1415f;
			Kart.MaxSteerAngle += 10f;
			// 차이값이라면 0~4 정도, 절대값이라면 22~26 정도라서 12 이상이면 이미 절대값으로 판단
			if (Kart.SteerConstraint >= 12f) notes += ", SteerConstraint 는 절대값(" + Kart.SteerConstraint + ")으로 판단";
			else Kart.SteerConstraint += 22.25f;
			Kart.FrontGripFactor += 5f;
			Kart.RearGripFactor += 5f;
			Kart.DriftTriggerFactor += 0.2f;
			Kart.DriftTriggerTime += 0.2f;
			Kart.DriftSlipFactor += 0.2f;
			// 차이값이라면 최대 2400 정도, 절대값이라면 2600 이상이라서 2500 이상이면 이미 절대값으로 판단
			if (Kart.DriftEscapeForce >= 2500f) notes += ", DriftEscapeForce 는 절대값(" + Kart.DriftEscapeForce + ")으로 판단";
			else Kart.DriftEscapeForce += 2600f;
			Kart.CornerDrawFactor += 0.18f;
			Kart.DriftMaxGauge += 4300f;
			Kart.TransAccelFactor += -0.0045f;
			Kart.BoostAccelFactor += -0.006f;
			// 출발 가속: 게임 파일에는 '배율'로 들어 있어서 최종 가속력과 함께 계산 (P3563 StartForwardAccelForce 와 동일)
			float accel = Kart.ForwardAccelForce;
			Kart.StartForwardAccelForceItem = StartAccel(Kart.StartForwardAccelForceItem, accel);
			Kart.StartForwardAccelForceSpeed = StartAccel(Kart.StartForwardAccelForceSpeed, accel);
			return notes;
		}

		private static readonly Dictionary<decimal, decimal> StartOffsets = new Dictionary<decimal, decimal>
		{
			{ 1.65m, -158.679m }, { 1.7m, -171.212m }, { 1.8m, -191.644m }, { 1.85m, -204.276m }, { 1.9m, -211.547m }, { 2.1m, -240.324m }
		};

		private static float StartAccel(float factor, float accel)
		{
			if (factor == 0f) return accel;
			decimal f = (decimal)factor;
			decimal a = (decimal)accel;
			decimal off;
			if (StartOffsets.TryGetValue(f, out off)) return (float)(f * a + off);
			return (float)(f * a - 58.8m * f * f);
		}
	}
}
