import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    background_color: "#05051f",
    description:
      "Aqua Lifestyle Club membership, products, programmes and community.",
    display: "standalone",
    icons: [
      {
        sizes: "512x512",
        src: "/icon.png",
        type: "image/png",
      },
      {
        sizes: "192x192",
        src: "/icon1.png",
        type: "image/png",
      },
    ],
    name: "Aqua Lifestyle Club",
    short_name: "Aqua Lifestyle",
    start_url: "/",
    theme_color: "#05051f",
  };
}
