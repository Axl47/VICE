// Adapted from S1LSD 0.11.0, copyright 2026 S1LSD Contributors (MIT).
// Decompiled from the user-supplied archive; see licenses/S1LSD/LICENSE.txt.
#nullable disable
using System;
using System.Collections.Generic;
using S1LSD.Config;

namespace S1LSD.Core;

public sealed class TripMind
{
	public const float EpisodeThreshold = 0.62f;

	public const float CalmThreshold = 0.3f;

	private const float EnterSeconds = 2f;

	private const float CalmSeconds = 12f;

	private const float ImpulseSeconds = 30f;

	private const float ImpulseRate = 0.045f;

	private const float ReliefTau = 90f;

	private static readonly float[] DoseSensitivity = new float[4] { 0.55f, 0.75f, 1f, 1.25f };

	private BadTripConfig _config;

	private readonly List<float> _impulses = new List<float>();

	private float _impulseActive;

	private float _aboveFor;

	private float _belowFor;

	private float _cooldown;

	private float _heartPhase;

	public float Anxiety { get; private set; }

	public float Euphoria { get; private set; }

	public float Relief { get; private set; }

	public bool InEpisode { get; private set; }

	public float EpisodeSeconds { get; private set; }

	public float BadTripBlend { get; private set; }

	public float Unease { get; private set; }

	public float HeartBpm { get; private set; } = 72f;

	public float HeartPulse { get; private set; }

	public bool HasPendingWave
	{
		get
		{
			if (_impulses.Count <= 0)
			{
				return _impulseActive > 0f;
			}
			return true;
		}
	}

	public int EpisodeCount { get; private set; }

	public TripMind(BadTripConfig config)
	{
		_config = config;
	}

	public void SetConfig(BadTripConfig config)
	{
		_config = config;
	}

	public void Reset()
	{
		float num = (HeartPulse = 0f);
		float num3 = (Unease = num);
		float num5 = (BadTripBlend = num3);
		float num7 = (Relief = num5);
		float anxiety = (Euphoria = num7);
		Anxiety = anxiety;
		InEpisode = false;
		EpisodeSeconds = 0f;
		EpisodeCount = 0;
		HeartBpm = 72f;
		_impulses.Clear();
		_impulseActive = (_aboveFor = (_belowFor = (_cooldown = (_heartPhase = 0f))));
	}

	public void PlantWave(float tripSeconds)
	{
		_impulses.Add(MathF.Max(0f, tripSeconds));
	}

	public void Bump(float amount)
	{
		Anxiety = Math.Clamp(Anxiety + amount, 0f, 1f);
	}

	public void ForceEpisode()
	{
		Anxiety = MathF.Max(Anxiety, 0.85f);
		StartEpisode();
	}

	public void EndEpisode(bool exhausted = false)
	{
		_impulses.Clear();
		_impulseActive = 0f;
		if (!InEpisode)
		{
			Anxiety *= 0.5f;
			return;
		}
		InEpisode = false;
		EpisodeSeconds = 0f;
		Relief = 1f;
		_cooldown = MathF.Max(0f, _config.CooldownMinutes) * 60f;
		Anxiety *= (exhausted ? 0.45f : 0.6f);
		_aboveFor = (_belowFor = 0f);
	}

	public void Soothe(float amount)
	{
		Anxiety = Math.Clamp(Anxiety - MathF.Max(0f, amount), 0f, 1f);
	}

	public void Tick(float dt, float tripSeconds, PhaseId phase, float envelope, int doseIndex, in MindInputs input)
	{
		if (dt <= 0f)
		{
			return;
		}
		envelope = Math.Clamp(envelope, 0f, 1.2f);
		float num = DoseSensitivity[Math.Clamp(doseIndex, 0, 3)];
		float num2 = num * (0.3f + 0.7f * MathF.Min(envelope, 1f));
		float x = (_config.UseSetting ? Math.Clamp(input.Stress, 0f, 1f) : 0f);
		float num3 = (_config.UseSetting ? Math.Clamp(input.Comfort, 0f, 1f) : 0f);
		for (int num4 = _impulses.Count - 1; num4 >= 0; num4--)
		{
			if (tripSeconds >= _impulses[num4])
			{
				_impulses.RemoveAt(num4);
				_impulseActive = 30f;
			}
		}
		float num5 = MathF.Pow(x, 1.2f) * 0.045f * num2;
		if (phase == PhaseId.ComeUp)
		{
			num5 += 0.006f * num;
		}
		if (_impulseActive > 0f)
		{
			num5 += 0.045f * (0.7f + 0.3f * num);
			_impulseActive = MathF.Max(0f, _impulseActive - dt);
		}
		float num6 = 0.02f + 0.03f * num3 + (input.Breathing ? 0.06f : 0f) + (input.Sleeping ? 0.08f : 0f);
		if (InEpisode)
		{
			num6 -= 0.02f;
		}
		if (!_config.Enabled)
		{
			num6 += 0.05f;
		}
		Anxiety = Math.Clamp(Anxiety + (num5 - num6 * Anxiety) * dt, 0f, 1f);
		_cooldown = MathF.Max(0f, _cooldown - dt);
		bool flag = (uint)(phase - 1) <= 3u;
		bool flag2 = flag;
		if (!InEpisode)
		{
			_aboveFor = ((Anxiety >= 0.62f) ? (_aboveFor + dt) : 0f);
			if (_config.Enabled && flag2 && _cooldown <= 0f && _aboveFor >= 2f)
			{
				StartEpisode();
			}
		}
		else
		{
			EpisodeSeconds += dt;
			_belowFor = ((Anxiety <= 0.3f) ? (_belowFor + dt) : 0f);
			float num7 = MathF.Max(0f, _config.MinDurationMinutes) * 60f;
			float num8 = MathF.Max(num7, _config.DurationMinutes * 60f);
			float num9 = (input.Breathing ? (num7 * 0.5f) : num7);
			if (_belowFor >= 12f && EpisodeSeconds >= num9)
			{
				EndEpisode();
			}
			else if (EpisodeSeconds >= num8)
			{
				EndEpisode(exhausted: true);
			}
		}
		float num10 = 1f / MathF.Max(1f, _config.BlendSeconds);
		float target = (InEpisode ? Curves.Smoothstep(0.2f, 0.75f, Anxiety) : 0f);
		BadTripBlend = MoveTowards(BadTripBlend, target, dt * num10);
		Unease = (InEpisode ? 0f : Curves.Smoothstep(0.25f, 0.6f, Anxiety));
		Relief *= MathF.Exp((0f - dt) / 90f);
		float num11 = 1f - Anxiety;
		float value = MathF.Min(envelope, 1f) * num11 * num11 * (0.7f + 0.3f * num3) + 0.45f * Relief;
		Euphoria = MoveTowards(Euphoria, Math.Clamp(value, 0f, 1f), dt * 0.1f);
		float x2 = 66f + 10f * MathF.Min(envelope, 1f) + 60f * Anxiety;
		HeartBpm = MoveTowards(HeartBpm, MathF.Min(x2, 138f), dt * 6f);
		_heartPhase = (_heartPhase + dt * HeartBpm / 60f) % 1f;
		HeartPulse = HeartShape(_heartPhase);
	}

	private void StartEpisode()
	{
		if (!InEpisode)
		{
			InEpisode = true;
			EpisodeSeconds = 0f;
			EpisodeCount++;
			_aboveFor = (_belowFor = 0f);
		}
	}

	public static float HeartShape(float phase)
	{
		float num = Bump01(phase, 0f, 0.14f);
		float num2 = 0.6f * Bump01(phase, 0.22f, 0.12f);
		return Math.Clamp(num + num2, 0f, 1f);
	}

	private static float Bump01(float x, float start, float width)
	{
		float num = (x - start) / width;
		if (num <= 0f || num >= 1f)
		{
			return 0f;
		}
		return MathF.Sin(num * (float)Math.PI) * MathF.Sin(num * (float)Math.PI);
	}

	private static float MoveTowards(float current, float target, float maxDelta)
	{
		if (!(MathF.Abs(target - current) <= maxDelta))
		{
			return current + (float)MathF.Sign(target - current) * maxDelta;
		}
		return target;
	}
}
