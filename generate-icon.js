const sharp = require('sharp');
const toIco = require('to-ico');
const fs = require('fs');
const path = require('path');

const inputPath = path.join(__dirname, 'assets', 'icon.png');
const outputIco = path.join(__dirname, 'assets', 'app.ico');

// Rozmiary ikon potrzebne dla Windows ICO
const sizes = [16, 24, 32, 48, 64, 128, 256];

async function generateIco() {
    console.log('🎨 Generowanie ikony ICO z:', inputPath);

    try {
        // Wczytaj oryginalny obraz i przygotuj bufor dla każdego rozmiaru
        const pngBuffers = await Promise.all(
            sizes.map(size =>
                sharp(inputPath)
                    .resize(size, size, {
                        fit: 'contain',
                        background: { r: 255, g: 255, b: 255, alpha: 0 }
                    })
                    .png()
                    .toBuffer()
            )
        );

        console.log('✅ Przygotowano', pngBuffers.length, 'rozmiarów');

        // Konwertuj do ICO
        const icoBuffer = await toIco(pngBuffers);

        // Zapisz plik ICO
        fs.writeFileSync(outputIco, icoBuffer);

        console.log('✔ Ikona ICO zapisana:', outputIco);
        console.log('📦 Rozmiar pliku:', Math.round(fs.statSync(outputIco).size / 1024), 'KB');

    } catch (error) {
        console.error('✖ Błąd:', error.message);
        process.exit(1);
    }
}

generateIco();
