// Procedurally generates a royalty-free greybox test track:
// 120 BPM, kick on every quarter, hats on eighths, a simple bassline.
// 16-bit mono PCM WAV. Strong, regular onsets = good for validating beat detection.
//
// Run from the repo root with .NET 10's file-based runner:
//   dotnet run tools/audio-gen/GenerateSampleTrack.cs
// Output: Assets/Audio/sample_beat.wav

using System;
using System.IO;
using System.Text;

const int SampleRate = 44100;
const double Duration = 30.0;
const double Bpm = 120.0;
const string OutPath = "Assets/Audio/sample_beat.wav";

int n = (int)(SampleRate * Duration);
double beat = 60.0 / Bpm; // seconds per quarter note
double[] buf = new double[n];

void AddKick(double t)
{
    int start = (int)(t * SampleRate);
    int len = (int)(0.18 * SampleRate);
    for (int i = 0; i < len && start + i < n; i++)
    {
        double tt = i / (double)SampleRate;
        double env = Math.Exp(-tt * 30.0);
        double freq = 120.0 * Math.Exp(-tt * 12.0) + 45.0; // pitch drop -> punchy kick
        buf[start + i] += Math.Sin(2.0 * Math.PI * freq * tt) * env * 0.9;
    }
}

void AddHat(double t, int seed)
{
    int start = (int)(t * SampleRate);
    int len = (int)(0.05 * SampleRate);
    var rnd = new Random(seed);
    for (int i = 0; i < len && start + i < n; i++)
    {
        double tt = i / (double)SampleRate;
        double env = Math.Exp(-tt * 80.0);
        buf[start + i] += (rnd.NextDouble() * 2.0 - 1.0) * env * 0.22;
    }
}

void AddBass(double t, double freq, double len)
{
    int start = (int)(t * SampleRate);
    int ln = (int)(len * SampleRate);
    for (int i = 0; i < ln && start + i < n; i++)
    {
        double tt = i / (double)SampleRate;
        double env = Math.Min(1.0, tt * 50.0) * Math.Exp(-tt * 3.0);
        buf[start + i] += Math.Sin(2.0 * Math.PI * freq * tt) * env * 0.35;
    }
}

double[] bassNotes = { 55.00, 55.00, 82.41, 73.42 }; // A1, A1, E2, D2 (one per bar)
int hatSeed = 1;
for (double t = 0; t < Duration; t += beat)
{
    AddKick(t);
    AddHat(t, hatSeed++);
    AddHat(t + beat / 2.0, hatSeed++);
    int barIdx = (int)(t / (beat * 4.0)) % bassNotes.Length;
    AddBass(t, bassNotes[barIdx], beat * 0.9);
}

// Normalize to -0.5 dBFS-ish.
double max = 0.0;
for (int i = 0; i < n; i++) max = Math.Max(max, Math.Abs(buf[i]));
double gain = max > 0.0 ? 0.95 / max : 1.0;

short[] samples = new short[n];
for (int i = 0; i < n; i++) samples[i] = (short)(buf[i] * gain * 32767.0);

using var fs = new FileStream(OutPath, FileMode.Create);
using var bw = new BinaryWriter(fs);
int byteRate = SampleRate * 2;
bw.Write(Encoding.ASCII.GetBytes("RIFF"));
bw.Write(36 + n * 2);
bw.Write(Encoding.ASCII.GetBytes("WAVE"));
bw.Write(Encoding.ASCII.GetBytes("fmt "));
bw.Write(16);              // fmt chunk size
bw.Write((short)1);        // PCM
bw.Write((short)1);        // mono
bw.Write(SampleRate);
bw.Write(byteRate);
bw.Write((short)2);        // block align
bw.Write((short)16);       // bits per sample
bw.Write(Encoding.ASCII.GetBytes("data"));
bw.Write(n * 2);
for (int i = 0; i < n; i++) bw.Write(samples[i]);

Console.WriteLine($"Wrote {OutPath} ({n} samples, {Duration}s, {SampleRate}Hz mono)");
