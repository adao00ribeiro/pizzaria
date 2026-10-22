import { Injectable, signal } from '@angular/core';

const SOUND_STORAGE_KEY = 'pz_kitchen_sound_enabled';

/**
 * Aviso sonoro de pedido novo no painel do pizzaiolo (premissa P10, docs/10 Sprint 3):
 * - Padrão ligado, com toggle persistido em `localStorage`.
 * - Sem autoplay bloqueante: destrava após o primeiro toque/interação na tela.
 * - Usa Web Audio API e possui fallback `<audio>` com WAV PCM local embutido.
 */
@Injectable({ providedIn: 'root' })
export class KitchenSoundService {
  readonly enabled = signal<boolean>(this.readPreference());
  readonly unlocked = signal<boolean>(false);

  private audioCtx: AudioContext | null = null;
  private fallbackAudio: HTMLAudioElement | null = null;

  toggle(): void {
    const next = !this.enabled();
    this.enabled.set(next);
    try {
      localStorage.setItem(SOUND_STORAGE_KEY, next ? '1' : '0');
    } catch {
      // Ignora ambientes sem storage disponível
    }
    if (next) {
      this.unlock();
      this.playNewOrderAlert();
    }
  }

  /**
   * Destrava o contexto de áudio após o primeiro gesto do usuário (toque/clique/tecla),
   * respeitando a política de autoplay dos navegadores mobile/tablet.
   */
  unlock(): void {
    if (this.unlocked()) {
      return;
    }
    try {
      const Ctx =
        typeof window !== 'undefined'
          ? window.AudioContext ||
            (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
          : undefined;
      if (Ctx && !this.audioCtx) {
        this.audioCtx = new Ctx();
      }
      if (this.audioCtx && this.audioCtx.state === 'suspended') {
        void this.audioCtx.resume();
      }
      if (!this.fallbackAudio && typeof Audio !== 'undefined') {
        this.fallbackAudio = new Audio(this.buildBellWavDataUri());
        this.fallbackAudio.preload = 'auto';
      }
      this.unlocked.set(true);
    } catch {
      // Silencioso caso o navegador não suporte áudio
    }
  }

  /** Toca uma campainha dupla de cozinha quando chega pedido novo. */
  playNewOrderAlert(): void {
    if (!this.enabled()) {
      return;
    }
    try {
      if (this.audioCtx && this.audioCtx.state === 'running') {
        const now = this.audioCtx.currentTime;
        this.scheduleTone(this.audioCtx, 880, now, 0.16);
        this.scheduleTone(this.audioCtx, 1174.66, now + 0.18, 0.28);
        return;
      }
      if (this.fallbackAudio) {
        this.fallbackAudio.currentTime = 0;
        void this.fallbackAudio.play().catch(() => undefined);
      }
    } catch {
      // Nunca interrompe o fluxo da fila por falha de áudio
    }
  }

  private scheduleTone(
    ctx: AudioContext,
    frequency: number,
    startTime: number,
    duration: number,
  ): void {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(frequency, startTime);

    gain.gain.setValueAtTime(0.001, startTime);
    gain.gain.exponentialRampToValueAtTime(0.28, startTime + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.001, startTime + duration);

    osc.connect(gain);
    gain.connect(ctx.destination);

    osc.start(startTime);
    osc.stop(startTime + duration + 0.02);
  }

  private readPreference(): boolean {
    try {
      const saved = localStorage.getItem(SOUND_STORAGE_KEY);
      return saved === null ? true : saved === '1';
    } catch {
      return true;
    }
  }

  /** Gera um WAV PCM leve em memória para fallback `<audio>` sem depender de rede. */
  private buildBellWavDataUri(): string {
    const sampleRate = 8000;
    const durationSec = 0.35;
    const numSamples = Math.floor(sampleRate * durationSec);
    const buffer = new ArrayBuffer(44 + numSamples * 2);
    const view = new DataView(buffer);

    const writeAscii = (offset: number, text: string) => {
      for (let i = 0; i < text.length; i++) {
        view.setUint8(offset + i, text.charCodeAt(i));
      }
    };

    writeAscii(0, 'RIFF');
    view.setUint32(4, 36 + numSamples * 2, true);
    writeAscii(8, 'WAVE');
    writeAscii(12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, 1, true);
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, sampleRate * 2, true);
    view.setUint16(32, 2, true);
    view.setUint16(34, 16, true);
    writeAscii(36, 'data');
    view.setUint32(40, numSamples * 2, true);

    for (let i = 0; i < numSamples; i++) {
      const t = i / sampleRate;
      const freq = t < 0.16 ? 880 : 1174.66;
      const env = Math.exp(-6 * (t < 0.16 ? t : t - 0.16));
      const sample = Math.sin(2 * Math.PI * freq * t) * env * 0.35;
      view.setInt16(44 + i * 2, Math.max(-1, Math.min(1, sample)) * 32767, true);
    }

    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return `data:audio/wav;base64,${btoa(binary)}`;
  }
}
