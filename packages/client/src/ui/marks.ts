import { h } from './dom';

// Lore's marks in a text (Ana, 20/10 #15 and 22/10 #26, #28): «**así**» the key words, in white bold; «==así==»
// the objective, in the accent. Neither nests, nor runs across a sentence: a pass over each line will do.

/** `text` as nodes: its marked words in <strong> and its objective in <mark>, the marks gone. */
export function marked(text: string): (string | HTMLElement)[] {
  return text
    .split(/(\*\*.+?\*\*|==.+?==)/g)
    .filter(Boolean)
    .map((part) => (part.startsWith('**') ? h('strong', {}, part.slice(2, -2)) : part.startsWith('==') ? h('mark.goal', {}, part.slice(2, -2)) : part));
}
