import pool from '../utils/database';
import { v4 as uuidv4 } from 'uuid';
import fs from 'fs';
import path from 'path';
import { Tag } from './Tag';

export interface ProjectInterface {
  id: string;
  name: string;
  description: string | null;
  tags: string[] | null;
  thumbnailUrl: string | null;
  mediaUrls: string[] | null;
  status: string;
  created: Date | null;
}

export class Project implements ProjectInterface {
  id: string;
  name: string;
  description: string | null;
  tags: string[] | null;
  thumbnailUrl: string | null;
  mediaUrls: string[] | null;
  status: string;
  created: Date | null;

  constructor(
    id: string,
    name: string,
    description: string | null,
    tags: string[] | null,
    thumbnailUrl: string | null,
    mediaUrls: string[] | null,
    status: string,
    created: Date | null
  ) {
    this.id = id;
    this.name = name;
    this.description = description;
    this.tags = tags;
    this.thumbnailUrl = thumbnailUrl;
    this.mediaUrls = mediaUrls;
    this.status = status;
    this.created = created;
  }

  static async upload(project: Project, owner: string): Promise<void> {
    const connection = await pool.getConnection();
    try {
      await connection.beginTransaction();

      // Insert into projects
      await connection.query(
        'INSERT INTO projects (id, name, description, date, status, author) VALUES (?, ?, ?, ?, ?, ?)',
        [project.id, project.name, project.description, new Date().toISOString().split('T')[0], 'W', owner]
      );

      // Insert into thumbnails
      await connection.query(
        'INSERT INTO thumbnails (id, id_pro, name) VALUES (?, ?, ?)',
        [uuidv4(), project.id, project.thumbnailUrl]
      );

      // Insert into media
      if (project.mediaUrls && project.mediaUrls.length > 0) {
        const mediaValues = project.mediaUrls.map(media => [uuidv4(), project.id, media]);
        
        await connection.query(
          'INSERT INTO media (id, id_pro, name) VALUES ?',
          [mediaValues]
        );
      }

      // Check existing tags
      const existingTags: Record<string, string> = {};
      
      if (project.tags && project.tags.length > 0) {
        const tagNames = project.tags.map(tag => `'${tag.replace(/'/g, "''")}'`).join(',');
        
        const [existingTagRows] = await connection.query(
          `SELECT id, name FROM tags WHERE name IN (${tagNames})`
        );
        
        for (const row of existingTagRows as any[]) {
          existingTags[row.name] = row.id;
        }

        // Insert missing tags
        const newTags = project.tags.filter(tag => !existingTags[tag]);
        
        if (newTags.length > 0) {
          const newTagValues = newTags.map(tag => {
            const newId = uuidv4();
            existingTags[tag] = newId;
            return [newId, tag];
          });

          await connection.query(
            'INSERT INTO tags (id, name) VALUES ?',
            [newTagValues]
          );
        }

        // Insert into tagged table
        const taggedValues = project.tags.map(tag => [uuidv4(), project.id, existingTags[tag]]);
        
        await connection.query(
          'INSERT INTO taged (id, id_pro, id_tag) VALUES ?',
          [taggedValues]
        );
      }

      await connection.commit();
    } catch (error) {
      await connection.rollback();
      throw error;
    } finally {
      connection.release();
    }
  }

  static async getProject(id: string): Promise<Project> {
    const tags = await Tag.getAllForProject(id);
    const media = await this.getProjectMedia(id);

    const [rows] = await pool.query(
      'SELECT p.id, p.name, t.name AS thumbnail, p.description, p.date FROM projects p JOIN thumbnails t ON t.id_pro = p.id WHERE p.id = ?',
      [id]
    );
    
    const row = (rows as any[])[0];
    
    return new Project(
      id,
      row.name,
      row.description,
      tags,
      row.thumbnail,
      media,
      'A',
      new Date(row.date)
    );
  }

  static async getPreviewProjects(): Promise<Project[]> {
    const projects: Project[] = [];
    
    const [rows] = await pool.query(
      'SELECT p.id, p.name, t.name AS thumbnail, p.date FROM projects p JOIN thumbnails t ON t.id_pro = p.id WHERE p.status = ?',
      ['A']
    );

    for (const row of rows as any[]) {
      projects.push(new Project(
        row.id,
        row.name,
        null,
        await Tag.getAllForProject(row.id),
        row.thumbnail,
        null,
        'A',
        new Date(row.date)
      ));
    }

    return projects;
  }

  static async getPreviewProjectsByUser(token: string, username: string): Promise<Project[]> {
    if (!username) return [];

    const projects: Project[] = [];
    
    const [rows] = await pool.query(
      'SELECT p.id, p.name, t.name AS thumbnail, p.date, p.status FROM projects p JOIN thumbnails t ON t.id_pro = p.id WHERE p.author = ?',
      [username]
    );

    for (const row of rows as any[]) {
      projects.push(new Project(
        row.id,
        row.name,
        null,
        await Tag.getAllForProject(row.id),
        row.thumbnail,
        null,
        row.status,
        new Date(row.date)
      ));
    }

    return projects;
  }

  static async updateProjectStatus(id: string, status: string): Promise<void> {
    await pool.query(
      'UPDATE projects SET status = ? WHERE id = ?',
      [status, id]
    );
  }

  static async getCheckProjects(): Promise<string[]> {
    const [rows] = await pool.query('SELECT id FROM projects');
    return (rows as any[]).map(row => row.id);
  }

  static async getCheckProject(id: string): Promise<Project | null> {
    const [rows] = await pool.query(
      'SELECT p.id, p.name, t.name AS thumbnail, p.status FROM projects p JOIN thumbnails t ON t.id_pro = p.id WHERE p.id = ?',
      [id]
    );

    if ((rows as any[]).length === 0) {
      return null;
    }

    const row = (rows as any[])[0];
    
    return new Project(
      row.id,
      row.name,
      null,
      null,
      row.thumbnail,
      null,
      row.status,
      null
    );
  }

  static async getProjectMedia(id: string): Promise<string[]> {
    const [rows] = await pool.query(
      'SELECT name FROM media WHERE id_pro = ?',
      [id]
    );

    return (rows as any[]).map(row => row.name);
  }

  static async isProjectActive(id: string): Promise<boolean> {
    const [rows] = await pool.query(
      'SELECT status FROM projects WHERE id = ? LIMIT 1',
      [id]
    );

    if ((rows as any[]).length === 0) {
      return false;
    }

    return (rows as any[])[0].status === 'A';
  }

  static async updateProject(
    project: Project, 
    removedMedia: string[], 
    addedMedia: string[],
    thumbnail: string | null = null
  ): Promise<void> {
    const connection = await pool.getConnection();
    
    try {
      await connection.beginTransaction();

      // Update project details
      await connection.query(
        'UPDATE projects SET name = ?, description = ?, status = ? WHERE id = ?',
        [project.name, project.description, 'W', project.id]
      );

      // Handle removed media
      if (removedMedia.length > 0) {
        const placeholders = removedMedia.map(() => '?').join(',');
        
        await connection.query(
          `DELETE FROM media WHERE id_pro = ? AND name IN (${placeholders})`,
          [project.id, ...removedMedia.map(m => m.replace(/\//g, '\\'))]
        );

        // Delete files
        for (const media of removedMedia) {
          if (fs.existsSync(media)) {
            fs.unlinkSync(media);
          }
        }
      }

      // Handle added media
      if (addedMedia.length > 0) {
        const mediaValues = addedMedia.map(media => [
          uuidv4(), 
          project.id, 
          media.replace(/\//g, '\\')
        ]);
        
        await connection.query(
          'INSERT INTO media (id, id_pro, name) VALUES ?',
          [mediaValues]
        );
      }

      // Update thumbnail if provided
      if (thumbnail) {
        await connection.query(
          'UPDATE thumbnails SET name = ? WHERE id_pro = ?',
          [thumbnail, project.id]
        );
      }

      // Delete existing tag connections first
      await connection.query(
        'DELETE FROM taged WHERE id_pro = ?',
        [project.id]
      );

      // Skip tag processing if no tags
      if (!project.tags || project.tags.length === 0) {
        await connection.commit();
        return;
      }

      // Get existing tags from database for comparison
      const [existingTagRows] = await connection.query('SELECT id, name FROM tags');
      const existingTags: Record<string, string> = {};
      
      for (const row of existingTagRows as any[]) {
        existingTags[row.name] = row.id;
      }

      // Find missing tags that need to be created
      const missingTags = project.tags.filter(tag => !existingTags[tag]);
      
      // Insert missing tags if any
      if (missingTags.length > 0) {
        const tagValues = missingTags.map(tag => {
          const newId = uuidv4();
          existingTags[tag] = newId; // Add to our map for later use
          return [newId, tag, 0];
        });
        
        await connection.query(
          'INSERT INTO tags (id, name, added) VALUES ?',
          [tagValues]
        );
      }

      // Create connections between project and tags
      const taggedValues = project.tags.map(tag => [
        uuidv4(),
        project.id,
        existingTags[tag]
      ]);
      
      await connection.query(
        'INSERT INTO taged (id, id_pro, id_tag) VALUES ?',
        [taggedValues]
      );

      await connection.commit();
    } catch (error) {
      await connection.rollback();
      throw error;
    } finally {
      connection.release();
    }
  }
} 