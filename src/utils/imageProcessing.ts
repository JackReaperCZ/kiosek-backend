import sharp from 'sharp';
import path from 'path';
import fs from 'fs';
import { config } from '../config';

export async function compressThumbnail(filePath: string): Promise<string> {
  try {
    // Generate the output path with same filename
    const originalExt = path.extname(filePath);
    const fileDir = path.dirname(filePath);
    const fileNameWithoutExt = path.basename(filePath, originalExt);
    
    // We'll keep the same extension, compress quality, and resize
    const outputPath = filePath; // Overwrite the original file
    
    await sharp(filePath)
      .resize({
        width: config.maxThumbnailWidth,
        height: config.maxThumbnailHeight,
        fit: 'inside',
        withoutEnlargement: true
      })
      .jpeg({ quality: 80 })
      .toFile(outputPath + '.tmp');
    
    // Replace the original file with the compressed version
    fs.unlinkSync(filePath);
    fs.renameSync(outputPath + '.tmp', outputPath);
    
    return outputPath;
  } catch (error) {
    console.error('Error compressing thumbnail:', error);
    return filePath; // Return original path if compression fails
  }
}