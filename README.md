# 🖥️ Kiosek Backend

Backend service for the Kiosek project using Node.js, TypeScript, and MySQL.

---

## 🛠️ Prerequisites

Make sure you have the following installed:

- [Node.js](https://nodejs.org/) (v14 or later recommended)
- npm (comes bundled with Node.js)
- [MySQL](https://dev.mysql.com/downloads/) (v5.7 or later)

---

## 📁 Project Structure

```pgsql
kiosek-backend/
├── node_modules/
├── src/
│   ├── index.ts
│   ├── config.ts
│   ├── utils
│   │   ├── database.ts
│   │   ├── imageProcessing.ts
│   │   ├── sanitize.ts
│   │   └── validation.ts
│   └── models
│   │   ├── Project.ts
│   │   ├── Tag.ts
│   │   └── User.ts
├── uploads
│   ├── media
│   └── thumbnails
├── tsconfig.json
├── package.json
└── README.md
```

## 📦 Setup Instructions

Follow these steps to set up and run the project locally.

### 1️⃣ Clone the Repository

```bash
git clone https://github.com/JackReaperCZ/kiosek-backend.git
cd kiosek-backend
```

### 2️⃣ Install Dependencies

```bash
npm install
```

### 🗄️ Database Setup
#### 3.1 Start MySQL Server
Ensure your MySQL server is running.

#### 3.2 Run SQL Setup Script

```sql
CREATE DATABASE `kiosek`;

USE `kiosek`;

CREATE TABLE IF NOT EXISTS `admins` (
  `id` INT(11) NOT NULL AUTO_INCREMENT,
  `username` VARCHAR(255) NOT NULL,
  `datum_pridani` DATE NOT NULL DEFAULT (CURDATE()),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `projects` (
  `id` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  `description` LONGTEXT NOT NULL,
  `date` DATE NOT NULL DEFAULT (CURDATE()),
  `status` CHAR(1) NOT NULL DEFAULT 'W',
  `author` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `media` (
  `id` VARCHAR(255) NOT NULL,
  `id_pro` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_media_projects` (`id_pro`),
  CONSTRAINT `FK_media_projects` FOREIGN KEY (`id_pro`) REFERENCES `projects` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `tags` (
  `id` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  `added` BIT(1) NOT NULL DEFAULT b'0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `taged` (
  `id` VARCHAR(255) NOT NULL,
  `id_pro` VARCHAR(255) NOT NULL,
  `id_tag` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_taged_projects` (`id_pro`),
  KEY `FK_taged_tags` (`id_tag`),
  CONSTRAINT `FK_taged_projects` FOREIGN KEY (`id_pro`) REFERENCES `projects` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION,
  CONSTRAINT `FK_taged_tags` FOREIGN KEY (`id_tag`) REFERENCES `tags` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `thumbnails` (
  `id` VARCHAR(255) NOT NULL,
  `id_pro` VARCHAR(255) NOT NULL,
  `name` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `FK_thumbnail_projects` (`id_pro`),
  CONSTRAINT `FK_thumbnail_projects` FOREIGN KEY (`id_pro`) REFERENCES `projects` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
```

#### 3.3 Create a database user with permissions:

```sql
CREATE USER 'kiosek'@'localhost' IDENTIFIED BY 'my_password';
GRANT ALL PRIVILEGES ON kiosek.* TO 'kiosek'@'localhost';
FLUSH PRIVILEGES;
```

### ⚙️ Configuration
4️⃣ Edit config.ts
Update `src/config.ts` with your local values:

```javascript
export const config = {
    maxThumbnailWidth: 800, //Max width of thumbnail
    maxThumbnailHeight: 600, //Max height of thumbnail
    port: 5148, //Port of the server
    issuer: 'http://localhost:5148', //Backend domain
    audience: 'http://localhost:3000', //Frontend domain
    secretKey: 'your-secret-key-here', //Secret key for JWT
    //DB
    DB_HOST: '127.0.0.1',
    DB_USER: 'root',
    DB_PASSWORD: '',
    DATABASE: 'kiosek',
}
```

### 🏗️ Build Project
Builds the project to the `./dist` folder:

```bash
npm run build
```

### 🚀 Start Project
Run the backend server:

```bash
npm start
```

✅ You're all set!
The backend should now be running on `http://localhost:5148`.
