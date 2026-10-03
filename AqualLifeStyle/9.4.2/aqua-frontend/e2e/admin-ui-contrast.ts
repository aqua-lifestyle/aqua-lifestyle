import type { Page } from "@playwright/test";

// Computed sRGB compositing, including opacity/tinted ancestor surfaces. This is
// focused evidence, not a replacement for an assistive-technology/conformance audit.
export const auditTextContrast = (page: Page) => page.evaluate(() => {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 1;
  const context = canvas.getContext("2d", { willReadFrequently: true })!;
  const rgba = (value: string) => {
    context.clearRect(0, 0, 1, 1);
    context.fillStyle = value;
    context.fillRect(0, 0, 1, 1);
    const pixels = context.getImageData(0, 0, 1, 1).data;
    return [pixels[0] / 255, pixels[1] / 255, pixels[2] / 255, pixels[3] / 255];
  };
  const mix = (fg: number[], bg: number[]) => fg.slice(0, 3).map((v, i) => v * fg[3] + bg[i] * (1 - fg[3]));
  const lum = (color: number[]) => color.slice(0, 3).reduce((total, v, i) => total + [0.2126, 0.7152, 0.0722][i] * (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4), 0);
  const ratio = (a: number[], b: number[]) => (Math.max(lum(a), lum(b)) + 0.05) / (Math.min(lum(a), lum(b)) + 0.05);
  const failures: { text: string; ratio: number; foreground: string; background: number[]; classes: string }[] = [];
  let minimum = 100;
  let checked = 0;
  for (const element of document.querySelectorAll<HTMLElement>("main *, aside *, header *, [role=alert] *")) {
    if (!Array.from(element.childNodes).some(n => n.nodeType === Node.TEXT_NODE && n.textContent?.trim())) continue;
    if (!element.getClientRects().length || element.closest('[aria-hidden="true"], .sr-only, button:disabled')) continue;
    const style = getComputedStyle(element);
    if (style.visibility !== "visible") continue;
    const ancestors: HTMLElement[] = [];
    let parent: HTMLElement | null = element;
    while (parent) { ancestors.unshift(parent); parent = parent.parentElement; }
    let background = rgba(getComputedStyle(document.body).backgroundColor);
    let opacity = 1;
    for (const ancestor of ancestors) {
      const ancestorStyle = getComputedStyle(ancestor);
      background = [...mix(rgba(ancestorStyle.backgroundColor), background), 1];
      opacity *= Number(ancestorStyle.opacity);
    }
    // Gradient-backed avatar initials are checked separately across all hues.
    if (ancestors.some(a => getComputedStyle(a).backgroundImage !== "none")) continue;
    const foreground = rgba(style.color);
    foreground[3] *= opacity;
    const result = ratio(mix(foreground, background), background);
    if (!Number.isFinite(result)) throw new Error("Text contrast could not be determined.");
    const large = parseFloat(style.fontSize) >= 24 || parseFloat(style.fontSize) >= 18.6667 && Number(style.fontWeight) >= 700;
    minimum = Math.min(minimum, result);
    checked++;
    if (result < (large ? 3 : 4.5)) failures.push({ text: element.textContent!.trim().slice(0, 60), ratio: Number(result.toFixed(2)), foreground: style.color, background: background.slice(0, 3).map(v => Math.round(v * 255)), classes: element.className });
  }
  for (const element of document.querySelectorAll<HTMLElement>('main input, main select, main textarea, main .border-control-border, dialog[open] input, dialog[open] select, dialog[open] textarea, dialog[open] [role="button"]')) {
    if (!element.getClientRects().length || element.matches(':disabled, [type="hidden"], [type="checkbox"]')) continue;
    const style = getComputedStyle(element);
    if (parseFloat(style.borderTopWidth) === 0) continue;
    const ancestors: HTMLElement[] = [];
    let parent: HTMLElement | null = element;
    while (parent) { ancestors.unshift(parent); parent = parent.parentElement; }
    let background = rgba(getComputedStyle(document.body).backgroundColor);
    for (const ancestor of ancestors) background = [...mix(rgba(getComputedStyle(ancestor).backgroundColor), background), 1];
    const result = ratio(mix(rgba(style.borderTopColor), background), background);
    if (!Number.isFinite(result)) throw new Error("Control-boundary contrast could not be determined.");
    if (result < 3) failures.push({ text: "Control boundary", ratio: Number(result.toFixed(2)), foreground: style.borderTopColor, background: background.slice(0, 3).map(v => Math.round(v * 255)), classes: element.className });
    checked++;
  }
  return { checked, minimum, failures };
});

export const auditThemePairs = (page: Page) => page.evaluate(() => {
  const context = document.createElement("canvas").getContext("2d", { willReadFrequently: true })!;
  const rgb = (value: string) => {
    context.clearRect(0, 0, 1, 1);
    context.fillStyle = value;
    context.fillRect(0, 0, 1, 1);
    return [...context.getImageData(0, 0, 1, 1).data].slice(0, 3).map(v => v / 255);
  };
  const tokens = getComputedStyle(document.documentElement);
  const color = (token: string) => rgb(tokens.getPropertyValue(`--${token}`));
  const lum = (color: number[]) => color.reduce((total, v, i) => total + [0.2126, 0.7152, 0.0722][i] * (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4), 0);
  const contrast = (a: number[], b: number[]) => (Math.max(lum(a), lum(b)) + 0.05) / (Math.min(lum(a), lum(b)) + 0.05);
  const pairs: { label: string; ratio: number; required: number }[] = [];
  const add = (label: string, fg: number[], bg: number[], required = 4.5) => pairs.push({ label, ratio: contrast(fg, bg), required });
  for (const surface of ["background", "card", "muted"]) {
    for (const text of ["foreground", "muted-foreground"]) add(`${text} / ${surface}`, color(text), color(surface));
    add(`control-border / ${surface}`, color("control-border"), color(surface), 3);
    add(`focus / ${surface}`, color("accent"), color(surface), 3);
    for (const semantic of ["accent", "success", "warning", "error", "info"]) {
      // 5/10% are the rendered semantic surfaces; only danger buttons use 20%.
      for (const opacity of [0.05, 0.1]) add(`${semantic} / ${opacity} tint on ${surface}`, color(semantic === "accent" ? "accent-ink" : semantic), color(semantic).map((v, i) => v * opacity + color(surface)[i] * (1 - opacity)));
      if (semantic === "error") add(`danger hover on ${surface}`, color("error-ink"), color(semantic).map((v, i) => v * 0.2 + color(surface)[i] * 0.8));
    }
  }
  for (const semantic of ["accent", "success", "warning", "error", "info"]) add(`${semantic} filled`, color(`${semantic}-foreground`), color(semantic));
  add("primary hover", color("accent-foreground"), color("accent-dark"));
  add("secondary", color("background"), color("primary"));
  add("secondary hover", color("background"), color("primary-light"));
  return { pairs, failures: pairs.filter(pair => pair.ratio < pair.required) };
});
