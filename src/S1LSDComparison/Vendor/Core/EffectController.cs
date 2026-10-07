// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using System.Collections.Generic;
using S1LSD.Config;

namespace S1LSD.Core;

public sealed class EffectController
{
	private LsdConfig _config;

	private readonly int _seed;

	private readonly ValueNoise _phaseNoise;

	private readonly BreathOscillator _breath;

	private float[] _peaks = new float[17];

	private float[] _phaseWeights = new float[PhaseNames.All.Length];

	private float[] _tripWeights = new float[PhaseNames.All.Length];

	private float _tripPeakScale = 1f;

	private float[] _phaseAmplitudes = new float[PhaseNames.All.Length];

	private Dictionary<PhaseId, Dictionary<ChannelId, float>> _channelBias = new Dictionary<PhaseId, Dictionary<ChannelId, float>>();

	private float _tolerance;

	private bool _active;

	private bool _closedEyes;

	private float _elapsed;

	private float _duration;

	private int _dose;

	private readonly TripMind _mind;

	private MindInputs _mindInputs;

	private float _strength;

	private const float RedoseRampSeconds = 90f;

	private float _potency = 1f;

	private float _redoseFrom;

	private float _redoseT = -1f;

	private float _lastEnvelope;

	private long _sampleId;

	private readonly float?[] _forcedChannels = new float?[17];

	public ChannelSnapshot Channels { get; private set; } = ChannelSnapshot.Zero();

	public bool IsActive
	{
		get
		{
			if (!_active)
			{
				return HasForcedChannels;
			}
			return true;
		}
	}

	public bool IsBadTrip
	{
		get
		{
			if (_active)
			{
				return _mind.InEpisode;
			}
			return false;
		}
	}

	public bool BadTripRolled { get; private set; }

	public TripMind Mind => _mind;

	public bool? ClosedEyesOverride { get; set; }

	public bool IsClosedEyes => ClosedEyesOverride ?? _closedEyes;

	public bool Overdrive { get; set; }

	public int Dose => _dose;

	public float ElapsedSeconds => _elapsed;

	public float DurationSeconds => _duration;

	public float Tolerance => _tolerance;

	public float Breath => _breath.Value;

	public bool HasForcedChannels
	{
		get
		{
			for (int i = 0; i < _forcedChannels.Length; i++)
			{
				if (_forcedChannels[i].HasValue)
				{
					return true;
				}
			}
			return false;
		}
	}

	public float BreathHz => _breath.CurrentHz;

	public PhaseId Phase => CurrentPhase;

	public long SampleId => _sampleId;

	public int StartCount { get; private set; }

	public PhaseId CurrentPhase
	{
		get
		{
			if (!_active)
			{
				return PhaseId.Onset;
			}
			float num = Math.Clamp(_elapsed / MathF.Max(0.001f, _duration), 0f, 1f);
			float num2 = 0f;
			PhaseId[] all = PhaseNames.All;
			foreach (PhaseId phaseId in all)
			{
				num2 += _tripWeights[(int)phaseId];
				if (num < num2)
				{
					return phaseId;
				}
			}
			return PhaseId.Afterglow;
		}
	}

	public float BadTripBlend
	{
		get
		{
			if (!_active)
			{
				return 0f;
			}
			return _mind.BadTripBlend;
		}
	}

	public EffectController(LsdConfig config, int seed)
	{
		_config = config;
		_seed = seed;
		_phaseNoise = new ValueNoise(seed ^ 0x5EED);
		_breath = new BreathOscillator(config.Breath, seed);
		_mind = new TripMind(config.BadTrip);
		ApplyConfig(config);
	}

	public void ForceChannel(ChannelId id, float value)
	{
		_forcedChannels[(int)id] = Math.Clamp(value, 0f, 1f);
		Channels = ComputeChannels();
	}

	public void UnforceChannel(ChannelId id)
	{
		_forcedChannels[(int)id] = null;
		Channels = ComputeChannels();
	}

	public void ClearForcedChannels()
	{
		Array.Clear(_forcedChannels, 0, _forcedChannels.Length);
		Channels = ComputeChannels();
	}

	public bool IsChannelForced(ChannelId id, out float value)
	{
		float? num = _forcedChannels[(int)id];
		if (num.HasValue)
		{
			value = num.Value;
			return true;
		}
		value = 0f;
		return false;
	}

	public void Preview(int dose, PhaseId phase)
	{
		Stop();
		Start(dose, 0f, forceBadTrip: false);
		BadTripRolled = false;
		_mind.Reset();
		Array.Copy(_phaseWeights, _tripWeights, _tripWeights.Length);
		_tripPeakScale = 1f;
		float num = 0f;
		for (int i = 0; i < (int)phase; i++)
		{
			num += _tripWeights[i];
		}
		num += _tripWeights[(int)phase] * 0.5f;
		_elapsed = _duration * Math.Clamp(num, 0f, 0.999f);
		Channels = ComputeChannels();
	}

	public void SetClosedEyes(bool active)
	{
		_closedEyes = active;
	}

	// Comparison-only adaptation: hold the phase without resetting the breathing
	// oscillator or RNG. The independent display clock continues to animate noise.
	public void HoldPreviewPhase(PhaseId phase)
	{
		float progress = 0f;
		for (int i = 0; i < (int)phase; i++) progress += _tripWeights[i];
		progress += _tripWeights[(int)phase] * 0.5f;
		_elapsed = _duration * Math.Clamp(progress, 0f, 0.999f);
		Channels = ComputeChannels();
	}

	public void ApplyConfig(LsdConfig config)
	{
		_config = config;
		_breath.SetConfig(config.Breath);
		_mind.SetConfig(config.BadTrip);
		_phaseWeights = new float[PhaseNames.All.Length];
		_phaseAmplitudes = new float[PhaseNames.All.Length];
		_channelBias = new Dictionary<PhaseId, Dictionary<ChannelId, float>>();
		PhaseId[] all = PhaseNames.All;
		foreach (PhaseId phaseId in all)
		{
			string key = PhaseNames.Key(phaseId);
			_phaseWeights[(int)phaseId] = TableValue(config.Phases.Weights, key);
			_phaseAmplitudes[(int)phaseId] = TableValue(config.Phases.Amplitude, key);
		}
		if (!_active)
		{
			Array.Copy(_phaseWeights, _tripWeights, _tripWeights.Length);
		}
		if (config.Phases.ChannelBias != null)
		{
			foreach (KeyValuePair<string, Dictionary<string, float>> item in config.Phases.ChannelBias)
			{
				item.Deconstruct(out var key2, out var value);
				string key3 = key2;
				Dictionary<string, float> dictionary = value;
				PhaseId key4;
				try
				{
					key4 = PhaseNames.FromKey(key3);
				}
				catch (ArgumentException)
				{
					continue;
				}
				Dictionary<ChannelId, float> dictionary2 = new Dictionary<ChannelId, float>();
				foreach (KeyValuePair<string, float> item2 in dictionary)
				{
					item2.Deconstruct(out key2, out var value2);
					string key5 = key2;
					float value3 = value2;
					if (ChannelCount.TryResolve(key5, out var id))
					{
						dictionary2[id] = value3;
					}
				}
				if (dictionary2.Count > 0)
				{
					_channelBias[key4] = dictionary2;
				}
			}
		}
		LoadDose(_dose);
	}

	private Dictionary<string, float> DosePeaksFor(int dose)
	{
		if (!_config.Doses.TryGetValue(dose.ToString(), out DoseConfig value))
		{
			return null;
		}
		return value.Peaks;
	}

	public void LoadDose(int dose)
	{
		_dose = dose;
		_peaks = new float[17];
		Dictionary<string, float> dictionary = DosePeaksFor(dose);
		if (dictionary == null)
		{
			return;
		}
		string[] doseKeys = ChannelCount.DoseKeys;
		foreach (string text in doseKeys)
		{
			float value2;
			float value3;
			float value4;
			if (dictionary.TryGetValue(text, out var value))
			{
				_peaks[(int)ChannelCount.Resolve(text)] = value;
			}
			else if (text == "textureCrawl" && dictionary.TryGetValue("warp", out value2))
			{
				_peaks[13] = value2;
			}
			else if (text == "pupil" && dictionary.TryGetValue("halo", out value3))
			{
				_peaks[14] = value3;
			}
			else if (text == "synesthesia" && dictionary.TryGetValue("lfPulse", out value4))
			{
				_peaks[15] = value4;
			}
			else if (text == "closedEyes")
			{
				_peaks[16] = 0f;
			}
		}
	}

	public void Start(int dose, float tolerance, bool forceBadTrip)
	{
		if (_active)
		{
			Redose(dose, tolerance, forceBadTrip);
			return;
		}
		LoadDose(dose);
		_tolerance = Math.Clamp(tolerance, 0f, 1f);
		XorShift32 rng = MakeRngForDose(dose);
		float num = Math.Clamp(_config.Phases.TripVariation, 0f, 0.5f);
		float num2 = 0f;
		float num3 = 0f;
		for (int i = 0; i < _tripWeights.Length; i++)
		{
			num2 += _phaseWeights[i];
			_tripWeights[i] = _phaseWeights[i] * (1f + num * (rng.NextFloat() * 2f - 1f));
			num3 += _tripWeights[i];
		}
		if (num3 > 0f)
		{
			for (int j = 0; j < _tripWeights.Length; j++)
			{
				_tripWeights[j] *= num2 / num3;
			}
		}
		_tripPeakScale = 1f + num / 3f * (rng.NextFloat() * 2f - 1f);
		float num4 = DurationMinutesFor(dose) * 60f;
		_duration = MathF.Max(1f, num4 * ToleranceModel.DurationMultiplier(_tolerance, _config.Tolerance));
		_elapsed = 0f;
		_active = true;
		_potency = 1f;
		_redoseT = -1f;
		StartCount++;
		_breath.Reset();
		_mind.Reset();
		BadTripRolled = _config.BadTrip.Enabled && BadTripModel.Roll(dose, _config.BadTrip, ref rng);
		if (BadTripRolled)
		{
			float a = PhaseStart(PhaseId.ComeUp) + 0.3f * _tripWeights[1];
			float b = PhaseStart(PhaseId.Plateau) + 0.5f * _tripWeights[3];
			_mind.PlantWave(_duration * Curves.Lerp(a, b, rng.NextFloat()));
		}
		if (forceBadTrip)
		{
			_mind.ForceEpisode();
		}
		Channels = ComputeChannels();
	}

	private void Redose(int dose, float tolerance, bool forceBadTrip)
	{
		float num = 1f - 0.85f * Math.Clamp(tolerance, 0f, 1f);
		float num2 = (float)dose / MathF.Max(70f, _dose);
		_redoseFrom = _lastEnvelope;
		_redoseT = 0f;
		if (dose > _dose)
		{
			LoadDose(dose);
		}
		_potency = Math.Clamp(_potency + 0.35f * num * num2, 1f, 1.35f);
		_duration += DurationMinutesFor(dose) * 60f * 0.4f * num;
		StartCount++;
		XorShift32 rng = MakeRngForDose(dose);
		BadTripRolled = _config.BadTrip.Enabled && BadTripModel.Roll(dose, _config.BadTrip, ref rng);
		if (BadTripRolled)
		{
			_mind.PlantWave(_elapsed + 60f + 120f * rng.NextFloat());
		}
		_mind.Bump(0.08f);
		if (forceBadTrip)
		{
			_mind.ForceEpisode();
		}
		Channels = ComputeChannels();
	}

	private float PhaseStart(PhaseId phase)
	{
		float num = 0f;
		for (int i = 0; i < (int)phase; i++)
		{
			num += _tripWeights[i];
		}
		return num;
	}

	public void SetMindInputs(in MindInputs inputs)
	{
		_mindInputs = inputs;
	}

	public void Stop()
	{
		_active = false;
		_closedEyes = false;
		ClosedEyesOverride = null;
		ClearForcedChannels();
		Overdrive = false;
		_mind.Reset();
		BadTripRolled = false;
		_potency = 1f;
		_redoseT = -1f;
		_lastEnvelope = 0f;
		_strength = 0f;
		_elapsed = 0f;
		_duration = 0f;
		Channels = ChannelSnapshot.Zero();
		_breath.Reset();
	}

	public void SetTolerance(float tolerance)
	{
		_tolerance = Math.Clamp(tolerance, 0f, 1f);
	}

	public void EndBadTrip()
	{
		_mind.EndEpisode();
	}

	public void ForceBadTrip(bool active)
	{
		if (!_active && active)
		{
			Start(140, 0f, forceBadTrip: true);
			return;
		}
		if (active)
		{
			_mind.ForceEpisode();
		}
		else
		{
			_mind.EndEpisode();
		}
		Channels = ComputeChannels();
	}

	public void Tick(float deltaTime)
	{
		if (!_active)
		{
			if (HasForcedChannels)
			{
				_breath.Tick(deltaTime);
				Channels = ComputeChannels();
			}
			return;
		}
		_elapsed += deltaTime;
		_breath.Tick(deltaTime);
		if (_redoseT >= 0f)
		{
			_redoseT = ((_redoseT + deltaTime >= 90f) ? (-1f) : (_redoseT + deltaTime));
		}
		_mind.Tick(deltaTime, _elapsed, CurrentPhase, _strength, VisualMixer.DoseIndex(_dose), in _mindInputs);
		Channels = ComputeChannels();
		if (_elapsed >= _duration)
		{
			Stop();
		}
	}

	public void SootheBadTrip(float deltaSeconds)
	{
		_mind.Soothe(deltaSeconds * 0.01f);
	}

	public void SetBreathRateMultiplier(float mult)
	{
		_breath.RateMultiplier = Math.Clamp(mult, 0.2f, 3f);
	}

	public float DurationMinutesFor(int dose)
	{
		if (!_config.Doses.TryGetValue(dose.ToString(), out DoseConfig value))
		{
			return 0f;
		}
		return value.DurationMinutes;
	}

	private XorShift32 MakeRngForDose(int dose)
	{
		uint num = (uint)((_seed * 73856093) ^ (dose * 19349663) ^ _sampleId);
		return new XorShift32((num == 0) ? 1u : num);
	}

	private ChannelSnapshot ComputeChannels()
	{
		float[] array = new float[17];
		if (!_active)
		{
			for (int i = 0; i < array.Length; i++)
			{
				float? num = _forcedChannels[i];
				if (num.HasValue)
				{
					float valueOrDefault = num.GetValueOrDefault();
					array[i] = valueOrDefault;
				}
			}
			return new ChannelSnapshot(array);
		}
		PhaseId currentPhase = CurrentPhase;
		PhaseId phaseId = NextPhase(currentPhase);
		float num2 = PhaseProgress(currentPhase);
		float num3 = _phaseAmplitudes[(int)currentPhase];
		float num4 = 0.35f;
		if (num2 > 1f - num4 && phaseId != currentPhase)
		{
			float t = Curves.Smootherstep((num2 - (1f - num4)) / num4);
			num3 = Curves.Lerp(num3, _phaseAmplitudes[(int)phaseId], t);
		}
		float x = _elapsed * MathF.Max(0.0001f, _config.Phases.Noise.Frequency);
		float num5 = 1f + _phaseNoise.Fbm(x, 2) * _config.Phases.Noise.Amplitude;
		num3 = Math.Clamp(num3 * num5, 0f, 1.2f);
		float num6 = _phaseNoise.Fbm(_elapsed * 0.015f, 2) * 0.42f;
		if (currentPhase == PhaseId.ComeUp && num2 > 0.65f)
		{
			float num7 = (num2 - 0.65f) / 0.35f;
			num6 += MathF.Sin(num7 * (float)Math.PI) * 0.25f;
		}
		float num8 = Math.Clamp(1f + num6, 0.68f, 1.32f);
		num3 = Math.Clamp(num3 * num8 * _tripPeakScale, 0f, 1.25f);
		if (_redoseT >= 0f)
		{
			num3 = Curves.Lerp(_redoseFrom, num3, Curves.Smootherstep(_redoseT / 90f));
		}
		_lastEnvelope = num3;
		num3 *= _potency;
		float num9 = ToleranceModel.EffectMultiplier(_tolerance, _config.Tolerance);
		_strength = num3 * num9;
		float num10 = MathF.Max(0f, _config.Quality.VisualIntensityScale);
		float value = _breath.Value;
		float num11 = 1f + (_breath.Value - 0.5f) * 2f * _config.Breath.Depth;
		for (int j = 0; j < 17; j++)
		{
			ChannelId channelId = (ChannelId)j;
			if (channelId != ChannelId.Tunnel && channelId != ChannelId.ClosedEyes)
			{
				float num12 = _peaks[j];
				float num13 = BiasFor(currentPhase, channelId);
				float num14 = num3 * num12 * num13 * num9 * num10;
				if (channelId == ChannelId.Warp || channelId == ChannelId.TextureCrawl)
				{
					num14 *= num11;
				}
				if (channelId == ChannelId.LowFreqPulse)
				{
					num14 *= 0.5f + value;
				}
				array[j] = Math.Clamp(num14, 0f, 1f);
			}
		}
		if (IsClosedEyes)
		{
			array[16] = 1f;
			array[7] = Math.Clamp(MathF.Max(array[7], 0.8f * num10), 0f, 1f);
		}
		else
		{
			array[16] = 0f;
		}
		float badTripBlend = BadTripBlend;
		if (badTripBlend > 0f)
		{
			BadTripConfig badTrip = _config.BadTrip;
			array[0] = Math.Clamp(array[0] * Curves.Lerp(1f, badTrip.WarpMultiplier, badTripBlend), 0f, 1f);
			array[2] = Math.Clamp(array[2] * Curves.Lerp(1f, badTrip.HaloMultiplier, badTripBlend), 0f, 1f);
			array[4] = Math.Clamp(array[4] * Curves.Lerp(1f, badTrip.ColorDriftMultiplier, badTripBlend), 0f, 1f);
			array[12] = Math.Clamp(badTrip.TunnelPeak * badTripBlend * num10, 0f, 1f);
		}
		for (int k = 0; k < array.Length; k++)
		{
			float? num = _forcedChannels[k];
			if (num.HasValue)
			{
				float valueOrDefault2 = num.GetValueOrDefault();
				array[k] = valueOrDefault2;
			}
		}
		_sampleId++;
		return new ChannelSnapshot(array);
	}

	private float BiasFor(PhaseId phase, ChannelId channel)
	{
		if (_channelBias.TryGetValue(phase, out Dictionary<ChannelId, float> value) && value.TryGetValue(channel, out var value2))
		{
			return value2;
		}
		return 1f;
	}

	public float PhaseProgress(PhaseId phase)
	{
		if (!_active)
		{
			return 0f;
		}
		float num = MathF.Max(0.0001f, _duration);
		float num2 = Math.Clamp(_elapsed / num, 0f, 1f);
		float num3 = 0f;
		for (int i = 0; i < (int)phase; i++)
		{
			num3 += _tripWeights[i];
		}
		float num4 = _tripWeights[(int)phase];
		if (num4 <= 0.0001f)
		{
			return 1f;
		}
		return Math.Clamp((num2 - num3) / num4, 0f, 1f);
	}

	private static PhaseId NextPhase(PhaseId phase)
	{
		int num = (int)(phase + 1);
		if (num >= PhaseNames.All.Length)
		{
			return phase;
		}
		return (PhaseId)num;
	}

	private static float TableValue(Dictionary<string, float> table, string key)
	{
		if (!table.TryGetValue(key, out var value))
		{
			return 0f;
		}
		return value;
	}
}
