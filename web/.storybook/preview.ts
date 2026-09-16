import type { Preview } from "@storybook/react-vite";

// O CSS do produto, e nao um tema do Storybook: story com aparencia propria
// mostra uma tela que nao existe, e o ajuste feito nela nao vale no produto.
import "../src/estilo.css";

const preview: Preview = {
  parameters: {
    // O produto e escuro. Fundo claro aqui faria cada contraste parecer errado.
    backgrounds: {
      options: { produto: { name: "produto", value: "#0f1115" } },
    },
  },
  initialGlobals: {
    backgrounds: { value: "produto" },
  },
};

export default preview;
