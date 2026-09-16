import type { StorybookConfig } from "@storybook/react-vite";

/**
 * Storybook para ver os ESTADOS da tela sem subir backend (#170).
 *
 * O que custa caro no trabalho de tela nao e o componente, e chegar ao estado:
 * ver "lead sem leitura ainda" exigia Postgres, API, Vite e a conversa certa
 * montada pelo webhook. Aqui e um clique.
 */
const config: StorybookConfig = {
  stories: ["../src/**/*.stories.tsx"],
  framework: {
    name: "@storybook/react-vite",
    options: {},
  },
  // Sem addon nenhum de proposito. O objetivo e OLHAR os estados; cada addon
  // que entra e mais superficie para manter num front que decidiu ficar no
  // minimo.
  addons: [],

  core: {
    // Desligada explicitamente. E anonima e o projeto nao tem nada a esconder,
    // mas um produto que discute LGPD em quinze issues nao manda dado para
    // fora por padrao so porque a ferramenta vem assim.
    disableTelemetry: true,
  },
};

export default config;
