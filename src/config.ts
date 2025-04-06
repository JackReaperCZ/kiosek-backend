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