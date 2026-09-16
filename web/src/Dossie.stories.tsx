import type { Meta, StoryObj } from "@storybook/react-vite";
import { PainelDoDossie } from "./Dossie";
import {
  dossie,
  lacunas,
  objecaoPorComportamento,
  objecaoPorTexto,
  sinalDeCompra,
  sinalDeFuga,
} from "./exemplos";

/**
 * Um story por ESTADO, nao por componente (#170).
 *
 * O que interessa ver e "dossie sem nada", "dossie so com fuga", "dossie com
 * resistencia detectada por padrao" — e conseguir olhar os tres lado a lado sem
 * subir Postgres, API e a conversa certa pelo webhook.
 */
const meta = {
  title: "Dossiê/Painel",
  component: PainelDoDossie,
  parameters: { layout: "fullscreen" },
  decorators: [
    (Story) => (
      <div className="painel__lado" style={{ maxWidth: 560, height: "100vh" }}>
        <h2 className="painel__titulo">O que lemos</h2>
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof PainelDoDossie>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * 404 da API: o lead existe e ainda nao foi lido.
 *
 * NAO e a mesma coisa que dossie vazio, e a diferenca e o ponto: vazio
 * pareceria leitura feita que nao achou nada, e o vendedor leria "nenhum sinal"
 * como certeza.
 */
export const AindaNaoAnalisado: Story = {
  args: { dossie: null },
};

/** A leitura rodou e nao achou nada. Estado raro, e por isso facil de esquecer. */
export const LeituraSemAchados: Story = {
  args: {
    dossie: dossie({ temperatura: "Fria", direcao: "Estavel", resumo: "fria estavel" }),
  },
};

export const SoSinaisDeCompra: Story = {
  args: {
    dossie: dossie({
      temperatura: "Quente",
      direcao: "Esquentando",
      resumo: "quente e esquentando",
      sinaisDeCompra: [sinalDeCompra],
    }),
  },
};

export const SoSinaisDeFuga: Story = {
  args: {
    dossie: dossie({
      temperatura: "Morna",
      direcao: "Esfriando",
      resumo: "morna e esfriando",
      sinaisDeFuga: [sinalDeFuga],
    }),
  },
};

/** Resistencia que o cliente DISSE. O vendedor costuma ver essa sozinho. */
export const ResistenciaPorTexto: Story = {
  args: {
    dossie: dossie({
      temperatura: "Morna",
      direcao: "Esfriando",
      resumo: "morna e esfriando",
      objecoes: [objecaoPorTexto],
    }),
  },
};

/**
 * Resistencia que ninguem disse — a etiqueta "padrão" existe por isso.
 *
 * E a leitura que o vendedor NAO faria sozinho: ninguem percebe que as
 * respostas do cliente encolheram pela metade ao longo de tres dias.
 */
export const ResistenciaPorComportamento: Story = {
  args: {
    dossie: dossie({
      temperatura: "Morna",
      direcao: "Esfriando",
      resumo: "morna e esfriando",
      objecoes: [objecaoPorComportamento],
    }),
  },
};

/** Onde as lacunas ficam sozinhas: a parte mais util e a mais facil de esquecer. */
export const SoLacunas: Story = {
  args: { dossie: dossie({ lacunas }) },
};

/** Tudo junto, que e como o vendedor vai ver na maioria das vezes. */
export const Completo: Story = {
  args: {
    dossie: dossie({
      temperatura: "Morna",
      direcao: "Esfriando",
      resumo: "morna e esfriando",
      sinaisDeCompra: [sinalDeCompra],
      sinaisDeFuga: [sinalDeFuga],
      objecoes: [objecaoPorTexto, objecaoPorComportamento],
      lacunas,
    }),
  },
};

/**
 * Muita coisa ao mesmo tempo. Existe para expor o problema que so aparece no
 * volume: a partir de certo ponto as lacunas somem abaixo da dobra, e elas sao
 * justamente a parte mais util.
 */
export const CheioDemais: Story = {
  args: {
    dossie: dossie({
      temperatura: "Quente",
      direcao: "Esfriando",
      resumo: "quente e esfriando",
      sinaisDeCompra: [sinalDeCompra, { ...sinalDeCompra, descricao: "perguntou prazo de entrega" }],
      sinaisDeFuga: [
        sinalDeFuga,
        { ...sinalDeFuga, descricao: "respondeu em uma palavra depois de mensagem longa" },
      ],
      objecoes: [
        objecaoPorTexto,
        objecaoPorComportamento,
        { ...objecaoPorTexto, tipo: "Autoridade", descricao: "quem decide e o socio" },
        { ...objecaoPorTexto, tipo: "Preco", descricao: "comparou com o fornecedor atual" },
      ],
      lacunas,
    }),
  },
};
