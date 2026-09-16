import type { Meta, StoryObj } from "@storybook/react-vite";
import { PainelDaConversa } from "./Conversa";
import { falasComAudio, falasDeVariosDias, falasDoCafe } from "./exemplos";

const meta = {
  title: "Conversa/Painel",
  component: PainelDaConversa,
  parameters: { layout: "fullscreen" },
  decorators: [
    (Story) => (
      <div className="painel__lado" style={{ maxWidth: 560, height: "100vh" }}>
        <h2 className="painel__titulo">Conversa</h2>
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof PainelDaConversa>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Lead existe e ainda nao falou. Diferente do dossie, aqui vazio E a verdade. */
export const Vazia: Story = {
  args: { falas: [] },
};

export const Curta: Story = {
  args: { falas: falasDoCafe },
};

/**
 * Com audio nao transcrito.
 *
 * O marcador diz o que NAO foi feito — "[audio nao transcrito, 22s]" e nao
 * "[audio]", que pareceria um audio que o sistema ouviu.
 */
export const ComMidiaNaoTranscrita: Story = {
  args: { falas: falasComAudio },
};

/** Varios dias: e onde a separacao por data precisa aparecer. */
export const AoLongoDeDias: Story = {
  args: { falas: falasDeVariosDias },
};

/**
 * Fala longa de verdade. O cliente cola um texto inteiro do fornecedor atual, e
 * a quebra de linha e o limite da bolha tem que aguentar.
 */
export const ComFalaLonga: Story = {
  args: {
    falas: [
      ...falasDoCafe,
      {
        id: "00000000-0000-0000-0000-000000000099",
        autor: "Cliente",
        texto:
          "olha, o que eu recebi do meu fornecedor atual foi isso aqui:\n\n" +
          "bourbon amarelo, torra media, moagem pra prensa francesa, " +
          "entrega toda segunda, 12 quilos por mes, com desconto progressivo " +
          "a partir do terceiro mes. o valor fechado deu bem abaixo disso ai " +
          "que voce me passou, e por isso eu queria entender melhor a diferenca.",
        enviadaEm: "2026-09-15T12:30:00+00:00",
        midia: null,
      },
    ],
  },
};
