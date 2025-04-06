import jwt from 'jsonwebtoken';
import { Request } from 'express';
import pool from './database';
import { User } from '../models/User';
import { config } from '../config';
class Validation {
  private static tokens: Record<string, User> = {};
  private static readonly EXPIRATION_DAYS = 7;
  
  private static readonly SECRET_KEY = config.secretKey;

  createToken(user: User): string {
    const token = jwt.sign(
      {
        sub: user.username,
        name: user.name,
        email: user.email,
        jti: this.generateUuid()
      },
      Validation.SECRET_KEY,
      {
        issuer: config.issuer,
        audience: config.audience,
        expiresIn: `${Validation.EXPIRATION_DAYS}d`
      }
    );

    Validation.tokens[token] = user;
    return token;
  }

  checkToken(token: string): boolean {
    // Check if token exists in memory
    if (!Validation.tokens[token]) {
      return false;
    }

    try {
      // Validate token
      jwt.verify(token, Validation.SECRET_KEY, {
        issuer: config.issuer,
        audience: config.audience
      });
      
      return true;
    } catch (error) {
      // If token expired, remove it from our record
      if (error instanceof jwt.TokenExpiredError) {
        delete Validation.tokens[token];
      }
      
      console.error(error);
      return false;
    }
  }

  getUsername(token: string): string | null {
    if (!Validation.tokens[token]) {
      return null;
    }
    
    return Validation.tokens[token].username;
  }

  async isAdmin(token: string): Promise<boolean> {
    if (!Validation.tokens[token]) {
      return false;
    }
    
    const username = Validation.tokens[token].username;
    
    const [rows] = await pool.query(
      'SELECT 1 FROM admins WHERE username = ? LIMIT 1',
      [username]
    );
    
    return (rows as any[]).length > 0;
  }

  validateHeader(req: Request): string | null {
    const authHeader = req.headers.authorization;
    
    if (!authHeader) {
      return null;
    }
    
    const bearerPrefix = 'Bearer ';
    
    if (!authHeader.startsWith(bearerPrefix)) {
      return null;
    }
    
    return authHeader.substring(bearerPrefix.length).trim();
  }

  async isOwner(id: string, token: string): Promise<boolean> {
    if (!Validation.tokens[token]) {
      return false;
    }
    
    const username = Validation.tokens[token].username;
    
    const [rows] = await pool.query(
      'SELECT 1 FROM projects WHERE author = ? AND id = ? LIMIT 1',
      [username, id]
    );
    
    return (rows as any[]).length > 0;
  }

  async validateId(id: string): Promise<boolean> {
    const [rows] = await pool.query(
      'SELECT id FROM projects WHERE id = ? LIMIT 1',
      [id]
    );
    
    return (rows as any[]).length > 0;
  }

  async validateIdTag(id: string): Promise<boolean> {
    const [rows] = await pool.query(
      'SELECT id FROM tags WHERE id = ? LIMIT 1',
      [id]
    );
    
    return (rows as any[]).length > 0;
  }

  async getOwner(id: string): Promise<string> {
    const [rows] = await pool.query(
      'SELECT author FROM projects WHERE id = ?',
      [id]
    );
    
    return (rows as any[])[0].author;
  }

  private generateUuid(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }
}

export default new Validation(); 