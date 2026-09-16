import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    // A API em outra porta viraria CORS. O proxy evita configurar CORS no
    // backend so por causa do desenvolvimento — e CORS afrouxado em dev tem a
    // mania de sobreviver ate producao.
    proxy: {
      "/leads": "http://localhost:5000",
      "/saude": "http://localhost:5000",
    },
  },
});
