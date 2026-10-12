import type { Metadata } from "next";
import localFont from "next/font/local";
import { Navbar } from "@/src/components/app/navbar";
import { AQGreenV2DemoBanner } from "@/src/components/members/aqgreen-v2-demo-banner";
import { PilotBoundary } from "@/src/components/app/pilot-boundary";
import { WebVitals } from "@/src/shared/observability/web-vitals";

import { AppProviders } from "./providers";
import "./globals.css";

const geistSans = localFont({
  src: "./fonts/geist-latin-variable.woff2",
  weight: "100 900",
  style: "normal",
  display: "swap",
  variable: "--font-geist-sans",
});

const geistMono = localFont({
  src: "./fonts/geist-mono-latin-variable.woff2",
  weight: "100 900",
  style: "normal",
  display: "swap",
  variable: "--font-geist-mono",
});

export const metadata: Metadata = {
  applicationName: "Aqua Lifestyle Club",
  description: "Aqua Lifestyle Club membership and product portal",
  metadataBase: new URL("https://www.aqualifestyleclub.co.za"),
  openGraph: {
    description: "Aqua Lifestyle Club membership and product portal",
    siteName: "Aqua Lifestyle Club",
    title: "Aqua Lifestyle Club",
    type: "website",
  },
  title: "Aqua Lifestyle Club",
  twitter: {
    card: "summary_large_image",
    description: "Aqua Lifestyle Club membership and product portal",
    title: "Aqua Lifestyle Club",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        <WebVitals />
        <AppProviders>
          <Navbar />
          <AQGreenV2DemoBanner />
          <PilotBoundary>{children}</PilotBoundary>
        </AppProviders>
      </body>
    </html>
  );
}
