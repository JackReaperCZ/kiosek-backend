import express, { Request, Response } from 'express';
import cors from 'cors';
import path from 'path';
import multer from 'multer';
import { v4 as uuidv4 } from 'uuid';
import fs from 'fs';

// Import models and utilities
import validation from './utils/validation';
import { Tag } from './models/Tag';
import { Project } from './models/Project';
import { UserDataFetcher } from './models/User';
import { sanitizeObject, sanitizeHtml } from './utils/sanitize';
import { compressThumbnail } from './utils/imageProcessing';
import { config } from './config';

// Configure storage for file uploads
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    let uploadDir;
    if (file.fieldname === 'thumbnail') {
      uploadDir = path.join('uploads', 'thumbnails');
    } else { // media or addedMedia or any other media field
      uploadDir = path.join('uploads', 'media');
    }
    
    if (!fs.existsSync(uploadDir)) {
      fs.mkdirSync(uploadDir, { recursive: true });
    }
    cb(null, uploadDir);
  },
  filename: (req, file, cb) => {
    cb(null, uuidv4() + path.extname(file.originalname));
  }
});

const upload = multer({ 
  storage,
  fileFilter: (req, file, cb) => {
    cb(null, true);
  }
});

// Create Express application
const app = express();
const port = config.port;

// Middleware
app.use(express.json());
app.use(cors());

// Static files
app.use('/uploads', express.static(path.join(process.cwd(), 'uploads')));

// API Routes
app.get('/api/tags', async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (!validation.checkToken(token)) {
    return res.status(401).send();
  }
  
  const tags = await Tag.getAll();
  
  // Extract tag names
  const tagNames = tags.map(tag => tag.name);
  
  return res.json(tagNames);
});

app.get('/api/check/tags', async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (!validation.checkToken(token) && await validation.isAdmin(token)) {
    return res.status(401).send();
  }
  
  const tags = await Tag.getAll();
  
  return res.json(tags);
});

app.post('/api/check/tags/review', async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (!validation.checkToken(token) && await validation.isAdmin(token)) {
    return res.status(401).send();
  }
  
  const { id } = req.query;
  
  if (id && typeof id === 'string') {
    if (!await validation.validateIdTag(id)) return res.status(400).send();
    
    try {
      const { approved } = req.body;
      
      await Tag.updateTagStatus(id, approved);
      
      return res.status(200).send('OK');
    } catch (error) {
      return res.status(400).send();
    }
  }
  
  return res.status(400).send();
});

app.get('/api/check/projects', async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (!validation.checkToken(token) && await validation.isAdmin(token)) {
    return res.status(401).send();
  }
  
  const { id, examine } = req.query;
  let exam = false;
  
  if (examine && typeof examine === 'string') {
    try {
      exam = Boolean(JSON.parse(examine));
    } catch (error) {
      console.log('Invalid examine parameter');
    }
  }
  
  if (id && typeof id === 'string') {
    if (!await validation.validateId(id)) return res.status(400).send();
    
    if (exam) {
      const project = await Project.getProject(id);
      
      // Don't sanitize the entire object - handle each field appropriately
      const projectForm = {
        id: project.id,
        name: sanitizeHtml(project.name),
        // For description: if it contains HTML, we want to preserve it as-is
        description: project.description,
        tags: JSON.stringify(project.tags || []),
        thumbnail: project.thumbnailUrl,
        media: JSON.stringify(project.mediaUrls || [])
      };
      
      return res.json(projectForm);
    } else {
      const project = await Project.getCheckProject(id);
      
      if (!project) return res.status(400).send();
      
      let status = 'Unknown.';
      switch (project.status) {
        case 'W':
          status = 'Waiting for checkup.';
          break;
        case 'D':
          status = 'Denied.';
          break;
        case 'A':
          status = 'Approved.';
          break;
      }
      
      const projectForm = {
        id,
        name: sanitizeHtml(project.name),
        thumbnail: project.thumbnailUrl,
        status
      };
      
      return res.json(projectForm);
    }
  } else {
    const projects = await Project.getCheckProjects();
    return res.json(sanitizeObject(projects));
  }
});

app.post('/api/check/projects/review', async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (!validation.checkToken(token) && await validation.isAdmin(token)) {
    return res.status(401).send();
  }
  
  const { id } = req.query;
  
  if (id && typeof id === 'string') {
    if (!await validation.validateId(id)) return res.status(400).send();
    
    try {
      const { approved } = req.body;
      
      const status = approved ? 'A' : 'D';
      
      await Project.updateProjectStatus(id, status);
      
      return res.status(200).send('OK');
    } catch (error) {
      return res.status(400).send();
    }
  }
  
  return res.status(400).send();
});

// Handle project submission with file uploads - use any() instead of fields()
app.post('/api/project', upload.any(), async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token || !validation.checkToken(token)) {
    return res.status(401).send();
  }
  
  try {
    // Extract basic data
    const { name, description } = req.body;
    let tags: string[] = [];
    
    if (req.body.tags) {
      tags = JSON.parse(req.body.tags);
    }
    
    // Validate required fields
    if (!name || !description) {
      return res.status(400).json({ message: 'Name and description are required.' });
    }
    
    // Handle thumbnail
    let thumbnailPath: string | null = null;
    const files = req.files as Express.Multer.File[];
    
    // Find thumbnail file
    const thumbnailFile = files?.find(file => file.fieldname === 'thumbnail');
    if (thumbnailFile) {
      thumbnailPath = thumbnailFile.path;
      // Compress the thumbnail for faster loading
      thumbnailPath = await compressThumbnail(thumbnailPath);
    }
    
    // Handle media files - collect all files that aren't thumbnails
    const mediaPaths: string[] = [];
    files?.forEach(file => {
      if (file.fieldname !== 'thumbnail') {
        mediaPaths.push(file.path);
      }
    });
    
    // Create project
    const project = new Project(
      uuidv4(),
      name,
      description,
      tags,
      thumbnailPath,
      mediaPaths,
      'W',
      null
    );
    
    // Get owner from token
    const owner = validation.getUsername(token);
    if (!owner) return res.status(401).send();
    
    // Upload project
    await Project.upload(project, owner);
    
    return res.status(200).json({ message: 'Project submitted successfully' });
  } catch (error) {
    console.error(error);
    return res.status(500).json({ message: error instanceof Error ? error.message : 'An error occurred' });
  }
});

app.post('/api/auth/login', async (req: Request, res: Response) => {
  try {
    const { username, password } = req.body;
    
    if (!username || !password) {
      return res.status(401).send();
    }
    
    const user = await UserDataFetcher.getUserDataAsync(username, password);
    
    if (!user) {
      console.log('Failed to find user');
      return res.status(401).send();
    }
    
    const token = validation.createToken(user);
    
    return res.json({
      name: user.name,
      token
    });
  } catch (error) {
    console.error(error);
    return res.status(401).send();
  }
});

app.get('/api/auth/validate', (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (validation.checkToken(token)) {
    return res.status(200).send();
  }
  
  return res.status(401).send();
});

app.get('/api/auth/validateOwner', async (req: Request, res: Response) => {
  const { id } = req.query;
  
  if (id && typeof id === 'string') {
    if (!await validation.validateId(id)) return res.status(400).send();
    
    const token = validation.validateHeader(req);
    
    if (!token) return res.status(401).send();
    
    if (validation.checkToken(token) && await validation.isOwner(id, token)) {
      return res.status(200).send();
    }
  }
  
  return res.status(401).send();
});

// Also update the project update route to use any()
app.post('/api/project/update', upload.any(), async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token || !validation.checkToken(token)) {
    return res.status(401).send();
  }
  
  const { id } = req.query;
  
  if (!id || typeof id !== 'string') return res.status(400).send();
  if (!await validation.validateId(id)) return res.status(400).send();
  
  try {
    const { name, description, tags: tagsJson, changedThumbnail, removedMedia: removedMediaJson } = req.body;
    
    if (!name || !description) {
      return res.status(400).json({ message: 'Name and description are required.' });
    }
    
    const changeThumbnail = changedThumbnail && JSON.parse(changedThumbnail);
    const tags = tagsJson ? JSON.parse(tagsJson) : [];
    const removedMedia = removedMediaJson ? JSON.parse(removedMediaJson) : [];
    
    let thumbnailPath: string | null = null;
    const files = req.files as Express.Multer.File[];
    
    // Find thumbnail file
    if (changeThumbnail) {
      const thumbnailFile = files?.find(file => file.fieldname === 'thumbnail');
      if (thumbnailFile) {
        thumbnailPath = thumbnailFile.path;
        // Compress the thumbnail for faster loading
        thumbnailPath = await compressThumbnail(thumbnailPath);
      }
    }
    
    // Collect added media files
    const mediaPaths: string[] = [];
    files?.forEach(file => {
      if (file.fieldname.startsWith('addedMedia') || file.fieldname.startsWith('media_')) {
        mediaPaths.push(file.path);
      }
    });
    
    const project = new Project(
      id,
      name,
      description,
      tags,
      null,
      null,
      'W',
      null
    );
    
    const owner = validation.getUsername(token);
    if (!owner) return res.status(401).send();
    
    await Project.updateProject(project, removedMedia, mediaPaths, thumbnailPath);
    
    return res.status(200).json({ message: 'Project updated successfully' });
  } catch (error) {
    console.error(error);
    return res.status(500).json({ message: error instanceof Error ? error.message : 'An error occurred' });
  }
});

app.get('/api/auth/validate/admin', async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (validation.checkToken(token) && await validation.isAdmin(token)) {
    return res.status(200).send();
  }
  
  return res.status(401).send();
});

app.get('/api/preview/projects', async (req: Request, res: Response) => {
  const projects = await Project.getPreviewProjects();
  
  const previewProjects = projects.map(project => ({
    ID: project.id,
    Name: project.name,
    ThumbnailUrl: project.thumbnailUrl,
    Tags: project.tags,
    Created: project.created
  }));
  
  return res.json(previewProjects);
});

app.get('/api/preview/my-projects', async (req: Request, res: Response) => {
  const token = validation.validateHeader(req);
  
  if (!token || !validation.checkToken(token)) {
    return res.status(401).send();
  }
  
  const username = validation.getUsername(token);
  if (!username) return res.status(401).send();
  
  const projects = await Project.getPreviewProjectsByUser(token, username);
  
  const previewProjects = projects.map(project => {
    let status = 'Unknown.';
    
    switch (project.status) {
      case 'A':
        status = 'Approved.';
        break;
      case 'D':
        status = 'Denied.';
        break;
      case 'W':
        status = 'Waiting for check.';
        break;
    }
    
    return sanitizeObject({
      ID: project.id,
      Name: project.name,
      ThumbnailUrl: project.thumbnailUrl,
      Tags: project.tags,
      status,
      Created: project.created
    });
  });
  
  return res.json(previewProjects);
});

app.get('/api/project', async (req: Request, res: Response) => {
  const { id } = req.query;
  
  if (id && typeof id === 'string') {
    if (!await validation.validateId(id)) return res.status(400).send();
    if (!await Project.isProjectActive(id)) return res.status(400).send();
    
    const project = await Project.getProject(id);
    
    const projectForm = {
      id: project.id,
      name: sanitizeHtml(project.name),
      // Preserve HTML in description
      description: project.description,
      tags: JSON.stringify(project.tags || []),
      thumbnail: project.thumbnailUrl,
      media: JSON.stringify(project.mediaUrls || [])
    };
    
    return res.json(projectForm);
  }
  
  return res.status(400).send();
});

app.get('/api/project/edit', async (req: Request, res: Response) => {
  const { id } = req.query;
  const token = validation.validateHeader(req);
  
  if (!token) return res.status(401).send();
  
  if (id && typeof id === 'string') {
    if (!await validation.validateId(id)) return res.status(400).send();
    if (!await validation.isOwner(id, token)) return res.status(401).send();
    
    const project = await Project.getProject(id);
    
    const projectForm = {
      id: project.id,
      name: sanitizeHtml(project.name),
      // Preserve HTML in description
      description: project.description,
      tags: JSON.stringify(project.tags || []),
      thumbnail: project.thumbnailUrl,
      media: JSON.stringify(project.mediaUrls || [])
    };
    
    return res.json(projectForm);
  }
  
  return res.status(400).send();
});

// Start server
app.listen(Number(port), '0.0.0.0', () => {
  console.log(`Server running on port ${port}`);
});