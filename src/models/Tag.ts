import pool from '../utils/database';

export interface TagInterface {
  id: string;
  name: string;
  added: boolean;
}

export class Tag implements TagInterface {
  id: string;
  name: string;
  added: boolean;

  constructor(id: string, name: string, added: boolean) {
    this.id = id;
    this.name = name;
    this.added = added;
  }

  static async getAll(): Promise<Tag[]> {
    const [rows] = await pool.query('SELECT * FROM tags');
    const tags: Tag[] = [];
    
    for (const row of rows as any[]) {
      tags.push(new Tag(
        row.id,
        row.name,
        Boolean(row.added)
      ));
    }
    
    return tags;
  }
  
  static async getAllForProject(id: string): Promise<string[]> {
    const [rows] = await pool.query(
      'SELECT t.name FROM tags t JOIN taged ta ON t.id = ta.id_tag WHERE ta.id_pro = ?',
      [id]
    );
    
    const tags: string[] = [];
    
    for (const row of rows as any[]) {
      tags.push(row.name);
    }
    
    return tags;
  }
  
  static async updateTagStatus(id: string, status: boolean): Promise<void> {
    if (status) {
      await pool.query('UPDATE tags SET added = ? WHERE id = ?', [1, id]);
    } else {
      await pool.query('DELETE FROM tags WHERE id = ?', [id]);
    }
  }
} 