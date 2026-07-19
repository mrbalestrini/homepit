const fs = require("node:fs/promises");
const path = require("node:path");

const sharpModulePath = process.env.SHARP_MODULE_PATH;
const sharp = sharpModulePath ? require(sharpModulePath) : require("sharp");

const root = path.resolve(__dirname, "..");
const source = path.join(root, "docs", "brand", "vectors");
const publicBrand = path.join(root, "apps", "web", "public", "brand");
const app = path.join(root, "apps", "web", "src", "app");
const cream = "#F7F3E8";

async function renderSquare(fileName, size, markSize, output) {
  const mark = await sharp(path.join(source, fileName))
    .resize(markSize, markSize, { fit: "contain" })
    .png()
    .toBuffer();

  await sharp({
    create: { width: size, height: size, channels: 4, background: cream },
  })
    .composite([{ input: mark, gravity: "center" }])
    .png()
    .toFile(output);
}

async function main() {
  await fs.mkdir(publicBrand, { recursive: true });

  await Promise.all([
    renderSquare("organiza-club-mark-navy.svg", 32, 27, path.join(publicBrand, "favicon-32.png")),
    renderSquare("organiza-club-mark-navy.svg", 180, 142, path.join(publicBrand, "apple-touch-icon.png")),
    renderSquare("organiza-club-mark-navy.svg", 192, 146, path.join(publicBrand, "pwa-192.png")),
    renderSquare("organiza-club-mark-navy.svg", 512, 390, path.join(publicBrand, "pwa-512.png")),
    renderSquare("organiza-club-mark-navy.svg", 512, 308, path.join(publicBrand, "pwa-maskable-512.png")),
    renderSquare("organiza-club-host-navy.svg", 512, 430, path.join(publicBrand, "avatar.png")),
    renderSquare("organiza-club-mark-navy.svg", 512, 390, path.join(app, "icon.png")),
    renderSquare("organiza-club-mark-navy.svg", 180, 142, path.join(app, "apple-icon.png")),
  ]);

  const lockup = await sharp(path.join(source, "organiza-club-lockup-navy.svg"))
    .resize(960, 240, { fit: "contain" })
    .png()
    .toBuffer();

  const openGraph = await sharp({
    create: { width: 1200, height: 630, channels: 4, background: cream },
  })
    .composite([{ input: lockup, gravity: "center" }])
    .png()
    .toBuffer();

  await Promise.all([
    fs.writeFile(path.join(publicBrand, "open-graph-1200x630.png"), openGraph),
    fs.writeFile(path.join(app, "opengraph-image.png"), openGraph),
  ]);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
