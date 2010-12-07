#include "putty.h"
#include "puttymem.h"
#include "terminal.h"

#define EXPORT(rt) __declspec(dllexport) rt

void init_fonts_hdc( HDC hdc, int pick_width, int pick_height );

EXPORT(void) InitializePutty() {
	cfgtopalette();
	init_palette();
}

EXPORT(Terminal*) CreatePuttyTerminal( int w, int h ) {
	int i;
	Config config = {0};
	struct unicode_data* unicode;
	Terminal* terminal;

	unicode = snew(struct unicode_data);
	memset( unicode, 0, sizeof(struct unicode_data) );

	for ( i=0 ; i<256 ; ++i ) {
		unicode->unitab_ctrl[i]=i;
		unicode->unitab_line[i]=i;
	}

	terminal = term_init( &config, unicode, NULL );
	terminal->wrap = 1;
	term_size( terminal, h, w, 0 );

	return terminal;
}

EXPORT(termchar*) GetPuttyTerminalLine( Terminal* terminal, int y, int unused ) {
	termline* line = index234(terminal->screen,y);
	return line->chars;
}

static int once = 0;

EXPORT(void) PaintPuttyTerminal( Terminal* terminal, HDC context, int w, int h ) {
	Terminal* oldterm = term;
	term = terminal;
	if (!once) init_fonts_hdc( context, 12, 12 );
	once=1;
	term_paint( term, context, 0, 0, w, h, 1 );
	term = oldterm;
}

EXPORT(void) DestroyPuttyTerminal( Terminal* terminal ) {
	safefree( terminal->ucsdata );
	term_free(terminal);
}

EXPORT(void) SendPuttyTerminal( Terminal *terminal, int is_stderr, const char *data, int len ) {
	term_data( terminal, is_stderr, data, len );
}
